using Dapper;
using PoultryPro.Models.Entities;


public class BirdService(IDbConnectionFactory db) : IBirdService
{
    public async Task<IEnumerable<Batch>> GetAllAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Batch>(
            "SELECT * FROM vw_BatchDetails WHERE IsActive=1 ORDER BY PlacementDate DESC");
    }
    public async Task<Batch?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<Batch>(
            "SELECT * FROM vw_BatchDetails WHERE Id=@id", new { id });
    }
    public async Task<int> CreateAsync(Batch batch)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Batches(BatchCode,Breed,BatchType,FarmId,InitialCount,CurrentCount,MortalityCount,PlacementDate,HealthStatus,AverageWeightKg,TotalFeedKg)
              VALUES(@BatchCode,@Breed,
                     CASE WHEN @Breed LIKE '%Layer%' THEN 'Layer' WHEN @Breed LIKE '%Sonali%' THEN 'Sonali' ELSE 'Broiler' END,
                     @FarmId,@InitialCount,@CurrentCount,0,@PlacementDate,@HealthStatus,0,0);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", batch);
    }
    public async Task UpdateAsync(Batch batch)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE Batches SET HealthStatus=@HealthStatus,CurrentCount=@CurrentCount,
              MortalityCount=@MortalityCount,AverageWeightKg=@AverageWeightKg,
              TotalFeedKg=@TotalFeedKg WHERE Id=@Id", batch);
    }
    public async Task<IEnumerable<MortalityLog>> GetMortalityLogsAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<MortalityLog>(
            @"SELECT ml.*,b.BatchCode,b.FarmId,f.Name AS FarmName
              FROM MortalityLogs ml
              INNER JOIN Batches b ON ml.BatchId=b.Id
              INNER JOIN Farms f ON b.FarmId=f.Id
              ORDER BY ml.LogDate DESC");
    }
    public async Task AddMortalityLogAsync(MortalityLog log)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"INSERT INTO MortalityLogs(BatchId,LogDate,Count,Cause,Notes)
              VALUES(@BatchId,@LogDate,@Count,@Cause,@Notes);
              UPDATE Batches SET MortalityCount=MortalityCount+@Count,
              CurrentCount=CASE WHEN CurrentCount-@Count < 0 THEN 0 ELSE CurrentCount-@Count END WHERE Id=@BatchId;
              UPDATE Farms SET CurrentStock = ISNULL((SELECT SUM(CurrentCount) FROM Batches WHERE FarmId = Farms.Id AND IsActive = 1), 0)
              WHERE Id = (SELECT FarmId FROM Batches WHERE Id=@BatchId);", log);
    }
    public async Task<IEnumerable<WeightRecord>> GetWeightRecordsAsync(int batchId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<WeightRecord>(
            "SELECT * FROM WeightRecords WHERE BatchId=@batchId ORDER BY AgeInDays", new { batchId });
    }
    public async Task AddWeightRecordAsync(WeightRecord record)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"INSERT INTO WeightRecords(BatchId,RecordDate,AgeInDays,AvgWeightKg,SampleSize,UniformityPct)
              VALUES(@BatchId,@RecordDate,@AgeInDays,@AvgWeightKg,@SampleSize,@UniformityPct);
              UPDATE Batches SET AverageWeightKg=@AvgWeightKg WHERE Id=@BatchId;", record);
    }
}