using Dapper;
using PoultryPro.Models.Entities;

public interface IDiseaseLibraryService
{
    Task<List<DiseaseLibraryEntry>> GetAllAsync(string? category = null, string? q = null);
    Task<List<string>> GetCategoriesAsync();
    Task<DiseaseLibraryEntry?> GetByIdAsync(int id);
    Task<int> CreateAsync(DiseaseLibraryEntry e);
    Task UpdateAsync(DiseaseLibraryEntry e);
    /// <summary>Farm cases whose DiseaseName matches the library entry (exact name or name prefix, e.g. "Newcastle Disease (ND)").</summary>
    Task<List<DiseaseCase>> GetCasesAsync(string diseaseName);
    Task<int> CountAsync();
}

public class DiseaseLibraryService(IDbConnectionFactory db) : IDiseaseLibraryService
{
    // A farm case matches when its name equals the library name or starts with it (case names often carry a qualifier).
    private const string CaseMatch = "(dc.DiseaseName = l.Name OR dc.DiseaseName LIKE l.Name + ' %' OR l.Name LIKE dc.DiseaseName + ' (%')";

    public async Task<List<DiseaseLibraryEntry>> GetAllAsync(string? category = null, string? q = null)
    {
        using var conn = db.Create();
        var like = string.IsNullOrWhiteSpace(q) ? null : "%" + q.Trim() + "%";
        var rows = await conn.QueryAsync<DiseaseLibraryEntry>(
            $@"SELECT l.*,
                      (SELECT COUNT(*) FROM DiseaseCases dc WHERE {CaseMatch}) AS CaseCount,
                      (SELECT COUNT(*) FROM DiseaseCases dc WHERE {CaseMatch} AND dc.Status <> 'Resolved') AS ActiveCaseCount
               FROM DiseaseLibrary l
               WHERE (@category IS NULL OR l.Category = @category)
                 AND (@like IS NULL OR l.Name LIKE @like OR l.LocalName LIKE @like OR l.Symptoms LIKE @like
                      OR l.Treatment LIKE @like OR l.Prevention LIKE @like)
               ORDER BY CASE l.Severity WHEN 'Critical' THEN 0 WHEN 'High' THEN 1 WHEN 'Medium' THEN 2 ELSE 3 END, l.Name",
            new { category = string.IsNullOrWhiteSpace(category) ? null : category, like });
        return rows.ToList();
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<string>("SELECT DISTINCT Category FROM DiseaseLibrary ORDER BY Category");
        var list = rows.ToList();
        foreach (var c in new[] { "Viral", "Bacterial", "Parasitic", "Fungal", "Nutritional", "Environmental" })
            if (!list.Contains(c)) list.Add(c);
        return list;
    }

    public async Task<DiseaseLibraryEntry?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<DiseaseLibraryEntry>(
            $@"SELECT l.*, (SELECT COUNT(*) FROM DiseaseCases dc WHERE {CaseMatch}) AS CaseCount
               FROM DiseaseLibrary l WHERE l.Id = @id", new { id });
    }

    public async Task<int> CreateAsync(DiseaseLibraryEntry e)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO DiseaseLibrary (Name, LocalName, Category, Severity, IsZoonotic, IsNotifiable, Symptoms, Transmission,
                                          Prevention, Treatment, AgeGroup, MortalityRange)
              VALUES (@Name, @LocalName, @Category, @Severity, @IsZoonotic, @IsNotifiable, @Symptoms, @Transmission,
                      @Prevention, @Treatment, @AgeGroup, @MortalityRange);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", e);
    }

    public async Task UpdateAsync(DiseaseLibraryEntry e)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE DiseaseLibrary SET Name=@Name, LocalName=@LocalName, Category=@Category, Severity=@Severity,
                     IsZoonotic=@IsZoonotic, IsNotifiable=@IsNotifiable, Symptoms=@Symptoms, Transmission=@Transmission,
                     Prevention=@Prevention, Treatment=@Treatment, AgeGroup=@AgeGroup, MortalityRange=@MortalityRange
              WHERE Id=@Id", e);
    }

    public async Task<List<DiseaseCase>> GetCasesAsync(string diseaseName)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<DiseaseCase>(
            @"SELECT dc.*, b.BatchCode, b.FarmId, f.Name AS FarmName
              FROM DiseaseCases dc
              INNER JOIN Batches b ON dc.BatchId = b.Id
              INNER JOIN Farms f ON b.FarmId = f.Id
              WHERE dc.DiseaseName = @n OR dc.DiseaseName LIKE @n + ' %' OR @n LIKE dc.DiseaseName + ' (%'
              ORDER BY dc.ReportedDate DESC", new { n = diseaseName });
        return rows.ToList();
    }

    public async Task<int> CountAsync()
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DiseaseLibrary");
    }
}
