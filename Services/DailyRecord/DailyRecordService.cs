using Dapper;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

public class DailyRecordService(IDbConnectionFactory db, IBatchService batchService) : IDailyRecordService
{
    public async Task<IEnumerable<DailyFarmRecord>> GetByVisitAsync(int visitId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<DailyFarmRecord>(
            "SELECT * FROM vw_DailyRecordSummary WHERE VisitId=@visitId ORDER BY RecordDate DESC",
            new { visitId });
    }
    public async Task<IEnumerable<DailyFarmRecord>> GetAll()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<DailyFarmRecord>(
            "SELECT * FROM vw_DailyRecordSummary ORDER BY RecordDate DESC"
          );
    }
    public async Task<IEnumerable<DailyFarmRecord>> GetByBatchAsync(int batchId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<DailyFarmRecord>(
            "SELECT * FROM vw_DailyRecordSummary WHERE BatchId=@batchId ORDER BY RecordDate DESC",
            new { batchId });
    }

    public async Task<IEnumerable<DailyFarmRecord>> GetByFarmAsync(int farmId,
                                                                     DateTime? from = null,
                                                                     DateTime? to = null)
    {
        using var conn = db.Create();
        var sql = "SELECT * FROM vw_DailyRecordSummary WHERE FarmId=@farmId";
        if (from.HasValue) sql += " AND RecordDate>=@from";
        if (to.HasValue) sql += " AND RecordDate<=@to";
        sql += " ORDER BY RecordDate DESC";
        return await conn.QueryAsync<DailyFarmRecord>(sql, new { farmId, from, to });
    }

    public async Task<DailyFarmRecord?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<DailyFarmRecord>(
            "SELECT * FROM vw_DailyRecordSummary WHERE Id=@id", new { id });
    }

    public async Task<int> CreateAsync(DailyRecordCreateVm vm)
    {
        using var conn = db.Create();
        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO DailyFarmRecords
                (VisitId,BatchId,FarmId,RecordDate,BirdCount,MortalityCount,CullingCount,
                 FeedConsumptionKg,WaterConsumptionLtr,TemperatureC,HumidityPercent,
                 LightHours,AverageWeightGm,MedicineUsed,MedicineDosage,VaccineUsed,Remarks)
              VALUES
                (@VisitId,@BatchId,@FarmId,@RecordDate,@BirdCount,@MortalityCount,@CullingCount,
                 @FeedConsumptionKg,@WaterConsumptionLtr,@TemperatureC,@HumidityPercent,
                 @LightHours,@AverageWeightGm,@MedicineUsed,@MedicineDosage,@VaccineUsed,@Remarks);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new
            {
                VisitId = vm.VisitId > 0 ? vm.VisitId : (int?)null,
                vm.BatchId, vm.FarmId, vm.RecordDate, vm.BirdCount, vm.MortalityCount, vm.CullingCount,
                vm.FeedConsumptionKg, vm.WaterConsumptionLtr, vm.TemperatureC, vm.HumidityPercent,
                vm.LightHours, vm.AverageWeightGm, vm.MedicineUsed, vm.MedicineDosage, vm.VaccineUsed, vm.Remarks
            });

        // Update batch stats
        var avgKg = vm.AverageWeightGm.HasValue ? vm.AverageWeightGm.Value / 1000m : 0m;
        await batchService.UpdateStatsAsync(vm.BatchId, vm.BirdCount,
            vm.MortalityCount, vm.CullingCount, avgKg, vm.FeedConsumptionKg);
        return id;
    }

    public async Task UpdateAsync(DailyFarmRecord record)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE DailyFarmRecords SET
                RecordDate=@RecordDate, BirdCount=@BirdCount,
                MortalityCount=@MortalityCount, CullingCount=@CullingCount,
                FeedConsumptionKg=@FeedConsumptionKg, WaterConsumptionLtr=@WaterConsumptionLtr,
                TemperatureC=@TemperatureC, HumidityPercent=@HumidityPercent,
                LightHours=@LightHours, AverageWeightGm=@AverageWeightGm,
                MedicineUsed=@MedicineUsed, MedicineDosage=@MedicineDosage,
                VaccineUsed=@VaccineUsed, Remarks=@Remarks
              WHERE Id=@Id", record);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("DELETE FROM DailyFarmRecords WHERE Id=@id", new { id });
    }
}