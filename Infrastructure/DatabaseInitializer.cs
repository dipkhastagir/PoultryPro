using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace PoultryPro.Infrastructure;

/// <summary>Holds the result of the start-up database check (shared singleton).</summary>
public class DbStatus
{
    public bool IsReady { get; set; }
    public string ConnectionString { get; set; } = "";
    public string ServerUsed { get; set; } = "";
    public string? Error { get; set; }
    public List<string> Log { get; } = new();
    public DateTime? CheckedAt { get; set; }
}

/// <summary>
/// Creates and upgrades the PoultryPro database automatically:
///   1. finds a reachable SQL Server (configured server, then fallbacks such as .\SQLEXPRESS or LocalDB),
///   2. creates the database if it does not exist,
///   3. runs SQL/01..03 (idempotent schema, views, reference data),
///   4. runs SQL/04 (demo data) only when the Farms table is empty.
/// </summary>
public class DatabaseInitializer(IConfiguration config, IWebHostEnvironment env, DbStatus status, ILogger<DatabaseInitializer> logger)
{
    private static readonly Regex GoSplitter = new(@"^\s*GO\s*;?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UseStatement = new(@"^\s*USE\s+\[?\w+\]?\s*;?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<bool> RunAsync()
    {
        status.Log.Clear();
        status.Error = null;
        status.IsReady = false;
        status.CheckedAt = DateTime.Now;

        var configured = config.GetConnectionString("Default")
                         ?? "Server=localhost;Database=PoultryProDB;Trusted_Connection=True;TrustServerCertificate=True;";
        var autoSetup = config.GetValue("Database:AutoSetup", true);
        var seedDemo  = config.GetValue("Database:SeedDemoData", true);
        var fallbacks = config.GetSection("Database:FallbackServers").Get<string[]>()
                        ?? new[] { @".\SQLEXPRESS", @"(localdb)\MSSQLLocalDB" };

        var candidates = new List<string> { configured };
        var baseBuilder = new SqlConnectionStringBuilder(configured);
        var configuredServer = baseBuilder.DataSource;
        if (config.GetValue("Database:TryFallbackServers", true))
        {
            foreach (var server in fallbacks.Where(s => !string.Equals(s, configuredServer, StringComparison.OrdinalIgnoreCase)))
            {
                var b = new SqlConnectionStringBuilder(configured) { DataSource = server };
                candidates.Add(b.ConnectionString);
            }
        }

        Exception? lastError = null;
        foreach (var cs in candidates)
        {
            var builder = new SqlConnectionStringBuilder(cs) { ConnectTimeout = 8 };
            var dbName = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "PoultryProDB" : builder.InitialCatalog;
            builder.InitialCatalog = dbName;
            try
            {
                Note($"Trying SQL Server '{builder.DataSource}' ...");
                var master = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" };
                await using (var conn = new SqlConnection(master.ConnectionString))
                {
                    await conn.OpenAsync();
                    Note($"Connected to '{builder.DataSource}' (SQL Server {conn.ServerVersion}).");
                    if (autoSetup)
                    {
                        await using var check = new SqlCommand("SELECT DB_ID(@n)", conn);
                        check.Parameters.AddWithValue("@n", dbName);
                        if (await check.ExecuteScalarAsync() is DBNull or null)
                        {
                            Note($"Database '{dbName}' not found - creating it.");
                            await using var create = new SqlCommand($"CREATE DATABASE [{dbName.Replace("]", "]]")}]", conn) { CommandTimeout = 120 };
                            await create.ExecuteNonQueryAsync();
                        }
                    }
                }

                var finalCs = new SqlConnectionStringBuilder(builder.ConnectionString) { ConnectTimeout = 30 }.ConnectionString;
                if (autoSetup)
                {
                    await RunScriptAsync(finalCs, "01_Schema.sql");
                    await RunScriptAsync(finalCs, "02_Views.sql");
                    await RunScriptAsync(finalCs, "03_ReferenceData.sql");
                    if (seedDemo && await IsEmptyAsync(finalCs))
                        await RunScriptAsync(finalCs, "04_DemoData.sql");
                }
                else
                {
                    await using var conn = new SqlConnection(finalCs);
                    await conn.OpenAsync();
                }

                status.ConnectionString = finalCs;
                status.ServerUsed = builder.DataSource;
                status.IsReady = true;
                Note("Database ready.");
                return true;
            }
            catch (Exception ex)
            {
                lastError = ex;
                Note($"Failed on '{builder.DataSource}': {ex.Message}");
                logger.LogWarning(ex, "Database initialisation failed on {Server}", builder.DataSource);
            }
        }

        status.ConnectionString = configured;
        status.Error = lastError?.Message ?? "Unknown database error";
        return false;
    }

    private async Task<bool> IsEmptyAsync(string cs)
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Farms", conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0;
    }

    private async Task RunScriptAsync(string cs, string fileName)
    {
        var path = FindScript(fileName);
        if (path == null) { Note($"Script {fileName} not found - skipped."); return; }

        var sw = Stopwatch.StartNew();
        var text = await File.ReadAllTextAsync(path);
        var batches = GoSplitter.Split(text)
            .Select(b => UseStatement.Replace(b, ""))
            .Where(b => !string.IsNullOrWhiteSpace(b) && b.Split('\n').Any(l => !l.TrimStart().StartsWith("--") && l.Trim().Length > 0))
            .ToList();

        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        var i = 0;
        foreach (var batch in batches)
        {
            i++;
            try
            {
                await using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 600 };
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"{fileName}, batch {i}: {ex.Message}", ex);
            }
        }
        Note($"{fileName}: {batches.Count} batches executed in {sw.ElapsedMilliseconds} ms.");
    }

    private string? FindScript(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(env.ContentRootPath, "SQL", fileName),
            Path.Combine(AppContext.BaseDirectory, "SQL", fileName)
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private void Note(string message)
    {
        status.Log.Add($"{DateTime.Now:HH:mm:ss}  {message}");
        logger.LogInformation("{Message}", message);
    }
}

/// <summary>Sends every request to the database-setup page while the database is unavailable.</summary>
public class DbReadyMiddleware(RequestDelegate next, DbStatus status)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (!status.IsReady
            && !path.StartsWith("/Setup", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/img", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.Redirect("/Setup");
            return;
        }
        await next(ctx);
    }
}
