using System.Data;
using Dapper;
using PoultryPro.Models.Entities;

public interface IVaccineScheduleService
{
    Task<List<VaccineTemplate>> GetTemplatesAsync(string? batchType = null, bool activeOnly = false);
    Task<VaccineTemplate?> GetTemplateAsync(int id);
    Task<List<VaccineTemplateItem>> GetItemsAsync(int templateId);
    Task<int> CreateTemplateAsync(VaccineTemplate t);
    Task UpdateTemplateAsync(VaccineTemplate t);
    Task AddItemAsync(VaccineTemplateItem item);
    /// <summary>Removes an item and returns its template id (0 if not found).</summary>
    Task<int> RemoveItemAsync(int itemId);
    /// <summary>Runs dbo.sp_ApplyVaccineTemplate and returns the number of vaccination records inserted.</summary>
    Task<int> ApplyAsync(int batchId, int templateId);
}

public class VaccineScheduleService(IDbConnectionFactory db) : IVaccineScheduleService
{
    public async Task<List<VaccineTemplate>> GetTemplatesAsync(string? batchType = null, bool activeOnly = false)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<VaccineTemplate>(
            @"SELECT t.*,
                     (SELECT COUNT(*)    FROM VaccineScheduleItems i WHERE i.TemplateId = t.Id) AS ItemCount,
                     (SELECT MIN(AgeDay) FROM VaccineScheduleItems i WHERE i.TemplateId = t.Id) AS FirstAgeDay,
                     (SELECT MAX(AgeDay) FROM VaccineScheduleItems i WHERE i.TemplateId = t.Id) AS LastAgeDay
              FROM VaccineScheduleTemplates t
              WHERE (@batchType IS NULL OR t.BatchType = @batchType) AND (@activeOnly = 0 OR t.IsActive = 1)
              ORDER BY t.IsActive DESC, t.BatchType, t.Name", new { batchType, activeOnly });
        return rows.ToList();
    }

    public async Task<VaccineTemplate?> GetTemplateAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<VaccineTemplate>(
            @"SELECT t.*, (SELECT COUNT(*) FROM VaccineScheduleItems i WHERE i.TemplateId = t.Id) AS ItemCount
              FROM VaccineScheduleTemplates t WHERE t.Id = @id", new { id });
    }

    public async Task<List<VaccineTemplateItem>> GetItemsAsync(int templateId)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<VaccineTemplateItem>(
            "SELECT * FROM VaccineScheduleItems WHERE TemplateId = @templateId ORDER BY AgeDay, Id", new { templateId });
        return rows.ToList();
    }

    public async Task<int> CreateTemplateAsync(VaccineTemplate t)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO VaccineScheduleTemplates (Name, BatchType, Description, IsActive)
              VALUES (@Name, @BatchType, @Description, @IsActive);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", t);
    }

    public async Task UpdateTemplateAsync(VaccineTemplate t)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE VaccineScheduleTemplates SET Name=@Name, BatchType=@BatchType, Description=@Description, IsActive=@IsActive
              WHERE Id=@Id", t);
    }

    public async Task AddItemAsync(VaccineTemplateItem item)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"INSERT INTO VaccineScheduleItems (TemplateId, AgeDay, VaccineName, Route, DoseMl, Notes)
              VALUES (@TemplateId, @AgeDay, @VaccineName, @Route, @DoseMl, @Notes)", item);
    }

    public async Task<int> RemoveItemAsync(int itemId)
    {
        using var conn = db.Create();
        var templateId = await conn.ExecuteScalarAsync<int?>("SELECT TemplateId FROM VaccineScheduleItems WHERE Id=@itemId", new { itemId });
        if (templateId == null) return 0;
        await conn.ExecuteAsync("DELETE FROM VaccineScheduleItems WHERE Id=@itemId", new { itemId });
        return templateId.Value;
    }

    public async Task<int> ApplyAsync(int batchId, int templateId)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int?>("dbo.sp_ApplyVaccineTemplate",
            new { BatchId = batchId, TemplateId = templateId }, commandType: CommandType.StoredProcedure) ?? 0;
    }
}
