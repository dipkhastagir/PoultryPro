using System.Collections.Concurrent;
using System.Globalization;
using Dapper;
using PoultryPro.Models.Entities;

/// <summary>Key/value application settings stored in dbo.AppSettings (cached in memory).</summary>
public interface ISettingsService
{
    Task<string> GetAsync(string key, string defaultValue = "");
    Task<decimal> GetDecimalAsync(string key, decimal defaultValue);
    Task<IEnumerable<AppSetting>> GetAllAsync();
    Task SetAsync(string key, string value);
}

public class SettingsService(IDbConnectionFactory db) : ISettingsService
{
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static DateTime _loadedAt = DateTime.MinValue;

    private async Task EnsureLoadedAsync()
    {
        if (DateTime.Now - _loadedAt < TimeSpan.FromMinutes(5) && !Cache.IsEmpty) return;
        using var conn = db.Create();
        var rows = await conn.QueryAsync<AppSetting>("SELECT * FROM AppSettings");
        Cache.Clear();
        foreach (var r in rows) Cache[r.Key] = r.Value;
        _loadedAt = DateTime.Now;
    }

    public async Task<string> GetAsync(string key, string defaultValue = "")
    {
        await EnsureLoadedAsync();
        return Cache.TryGetValue(key, out var v) ? v : defaultValue;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue)
    {
        var v = await GetAsync(key, "");
        return decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : defaultValue;
    }

    public async Task<IEnumerable<AppSetting>> GetAllAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<AppSetting>("SELECT * FROM AppSettings ORDER BY GroupName, [Key]");
    }

    public async Task SetAsync(string key, string value)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"IF EXISTS (SELECT 1 FROM AppSettings WHERE [Key]=@key)
                  UPDATE AppSettings SET [Value]=@value, UpdatedAt=GETDATE() WHERE [Key]=@key
              ELSE
                  INSERT INTO AppSettings([Key],[Value]) VALUES(@key,@value)", new { key, value });
        Cache[key] = value;
    }
}
