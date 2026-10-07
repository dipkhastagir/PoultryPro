using Dapper;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

public interface IEggProductionService
{
    Task<List<Batch>> GetLayerBatchesAsync(bool includeClosed = true);
    Task<List<EggProductionRecord>> GetRecordsAsync(int? batchId, int days = 90);
    Task<EggProductionRecord?> GetByIdAsync(int id);
    Task<bool> ExistsAsync(int batchId, DateTime date, int excludeId = 0);
    Task<int> CreateAsync(EggProductionFormVm vm);
    Task UpdateAsync(EggProductionFormVm vm);
    Task<int> DeleteAsync(int id);
}

public class EggProductionService(IDbConnectionFactory db) : IEggProductionService
{
    public async Task<List<Batch>> GetLayerBatchesAsync(bool includeClosed = true)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<Batch>(
            @"SELECT * FROM vw_BatchDetails WHERE BatchType = 'Layer' AND (@includeClosed = 1 OR IsActive = 1)
              ORDER BY IsActive DESC, PlacementDate DESC", new { includeClosed });
        return rows.ToList();
    }

    public async Task<List<EggProductionRecord>> GetRecordsAsync(int? batchId, int days = 90)
    {
        using var conn = db.Create();
        var from = DateTime.Today.AddDays(-days + 1);
        var rows = await conn.QueryAsync<EggProductionRecord>(
            @"SELECT * FROM vw_EggProductionSummary
              WHERE (@batchId IS NULL OR BatchId = @batchId) AND RecordDate >= @from
              ORDER BY RecordDate DESC, BatchCode", new { batchId, from });
        return rows.ToList();
    }

    public async Task<EggProductionRecord?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<EggProductionRecord>("SELECT * FROM vw_EggProductionSummary WHERE Id=@id", new { id });
    }

    public async Task<bool> ExistsAsync(int batchId, DateTime date, int excludeId = 0)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM EggProductionRecords WHERE BatchId=@batchId AND RecordDate=@date AND Id<>@excludeId",
            new { batchId, date = date.Date, excludeId }) > 0;
    }

    public async Task<int> CreateAsync(EggProductionFormVm vm)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO EggProductionRecords (BatchId, RecordDate, HenCount, TotalEggs, GradeA, GradeB, CrackedEggs, DirtyEggs,
                                                AvgEggWeightGm, FeedKg, Notes)
              VALUES (@BatchId, @RecordDate, @HenCount, @TotalEggs, @GradeA, @GradeB, @CrackedEggs, @DirtyEggs,
                      @AvgEggWeightGm, @FeedKg, @Notes);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", vm);
    }

    public async Task UpdateAsync(EggProductionFormVm vm)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE EggProductionRecords SET BatchId=@BatchId, RecordDate=@RecordDate, HenCount=@HenCount, TotalEggs=@TotalEggs,
                     GradeA=@GradeA, GradeB=@GradeB, CrackedEggs=@CrackedEggs, DirtyEggs=@DirtyEggs,
                     AvgEggWeightGm=@AvgEggWeightGm, FeedKg=@FeedKg, Notes=@Notes
              WHERE Id=@Id", vm);
    }

    public async Task<int> DeleteAsync(int id)
    {
        using var conn = db.Create();
        var batchId = await conn.ExecuteScalarAsync<int?>("SELECT BatchId FROM EggProductionRecords WHERE Id=@id", new { id }) ?? 0;
        await conn.ExecuteAsync("DELETE FROM EggProductionRecords WHERE Id=@id", new { id });
        return batchId;
    }
}
