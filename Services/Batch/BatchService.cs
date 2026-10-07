using System.Data;
using Dapper;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;


public class BatchService(IDbConnectionFactory db) : IBatchService
{
    public async Task<IEnumerable<Batch>> GetAllByFarmAsync(int farmId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Batch>(
            "SELECT * FROM vw_BatchDetails WHERE FarmId=@farmId ORDER BY IsActive DESC, PlacementDate DESC",
            new { farmId });
    }

    public async Task<IEnumerable<Batch>> GetAllAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Batch>(
            "SELECT * FROM vw_BatchDetails ORDER BY FarmName, IsActive DESC, PlacementDate DESC");
    }

    public async Task<Batch?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<Batch>(
            "SELECT * FROM vw_BatchDetails WHERE Id=@id", new { id });
    }

    public async Task<int> CreateAsync(BatchCreateVm vm)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Batches
                (FarmId,BatchCode,Breed,BatchType,InitialCount,CurrentCount,
                 PlacementDate,ExpectedHarvestDate,HealthStatus,Notes,ShedNo,HatcheryName,ChickCostPerBird)
              VALUES
                (@FarmId,@BatchCode,@Breed,@BatchType,@InitialCount,@InitialCount,
                 @PlacementDate,@ExpectedHarvestDate,@HealthStatus,@Notes,@ShedNo,@HatcheryName,@ChickCostPerBird);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", vm);
    }

    public async Task UpdateAsync(Batch batch)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE Batches SET
                BatchCode=@BatchCode, Breed=@Breed, BatchType=@BatchType,
                CurrentCount=@CurrentCount, MortalityCount=@MortalityCount,
                CullingCount=@CullingCount, ExpectedHarvestDate=@ExpectedHarvestDate,
                HealthStatus=@HealthStatus, AverageWeightKg=@AverageWeightKg,
                TotalFeedKg=@TotalFeedKg, Notes=@Notes,
                ShedNo=@ShedNo, HatcheryName=@HatcheryName, ChickCostPerBird=@ChickCostPerBird
              WHERE Id=@Id", batch);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            "UPDATE Batches SET IsActive=0 WHERE Id=@id", new { id });
    }

    public async Task UpdateStatsAsync(int batchId, int currentCount, int mortalityDelta,
                                       int cullingDelta, decimal avgWeightKg, decimal feedAddedKg)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE Batches SET
                CurrentCount      = @currentCount,
                MortalityCount    = MortalityCount + @mortalityDelta,
                CullingCount      = CullingCount   + @cullingDelta,
                AverageWeightKg   = CASE WHEN @avgWeightKg > 0 THEN @avgWeightKg ELSE AverageWeightKg END,
                TotalFeedKg       = TotalFeedKg + @feedAddedKg
              WHERE Id = @batchId",
            new { batchId, currentCount, mortalityDelta, cullingDelta, avgWeightKg, feedAddedKg });
    }

    // ───────────────────────────── Flock lifecycle (v2) ─────────────────────────────

    public async Task<List<Batch>> GetRegisterAsync(int? farmId, string status)
    {
        using var conn = db.Create();
        var where = (status ?? "active").ToLowerInvariant() switch
        {
            "closed" => "IsActive = 0",
            "all"    => "1 = 1",
            _        => "IsActive = 1"
        };
        var rows = await conn.QueryAsync<Batch>(
            $@"SELECT * FROM vw_BatchPerformance
               WHERE {where} AND (@farmId IS NULL OR FarmId = @farmId)
               ORDER BY IsActive DESC, PlacementDate DESC", new { farmId });
        return rows.ToList();
    }

    public async Task<Batch?> GetPerformanceAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<Batch>("SELECT * FROM vw_BatchPerformance WHERE Id=@id", new { id });
    }

    public async Task<List<Batch>> GetActiveAsync(string? batchType = null)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<Batch>(
            @"SELECT * FROM vw_BatchDetails WHERE IsActive = 1 AND (@batchType IS NULL OR BatchType = @batchType)
              ORDER BY FarmName, PlacementDate DESC", new { batchType });
        return rows.ToList();
    }

    public async Task<List<FlockHealthFlag>> GetHealthFlagsAsync()
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<FlockHealthFlag>(
            @"SELECT b.Id AS BatchId,
                     (SELECT COUNT(*) FROM VaccinationRecords v WHERE v.BatchId = b.Id AND v.CompletedDate IS NULL
                         AND (v.Status = 'Overdue' OR v.DueDate < CAST(GETDATE() AS DATE))) AS OverdueVaccines,
                     (SELECT COUNT(*) FROM VaccinationRecords v WHERE v.BatchId = b.Id AND v.CompletedDate IS NULL
                         AND v.DueDate BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(DAY, 3, CAST(GETDATE() AS DATE))) AS DueSoonVaccines,
                     (SELECT COUNT(*) FROM DiseaseCases d WHERE d.BatchId = b.Id AND d.Status <> 'Resolved') AS ActiveDiseases
              FROM Batches b");
        return rows.ToList();
    }

    public async Task<string> SuggestNextCodeAsync()
    {
        using var conn = db.Create();
        var prefix = "B-" + DateTime.Today.ToString("yy");
        var codes = (await conn.QueryAsync<string>(
            "SELECT BatchCode FROM Batches WHERE BatchCode LIKE @p", new { p = prefix + "%" })).ToList();
        var max = 0;
        foreach (var c in codes)
            if (int.TryParse(c.Substring(prefix.Length), out var n) && n > max) max = n;
        string candidate;
        var next = max + 1;
        do
        {
            candidate = prefix + next.ToString("00");
            next++;
        } while (codes.Contains(candidate, StringComparer.OrdinalIgnoreCase));
        return candidate;
    }

    public async Task<bool> CodeExistsAsync(string code, int excludeId = 0)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Batches WHERE BatchCode = @code AND Id <> @excludeId", new { code, excludeId }) > 0;
    }

    public async Task<(int BatchId, int VaccinesScheduled)> CreateFlockAsync(BatchCreateVm vm)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Batches
                (FarmId,BatchCode,Breed,BatchType,InitialCount,CurrentCount,
                 PlacementDate,ExpectedHarvestDate,HealthStatus,Notes,ShedNo,HatcheryName,ChickCostPerBird)
              VALUES
                (@FarmId,@BatchCode,@Breed,@BatchType,@InitialCount,@InitialCount,
                 @PlacementDate,@ExpectedHarvestDate,@HealthStatus,@Notes,@ShedNo,@HatcheryName,@ChickCostPerBird);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", vm, tx);

        if (vm.PostChickPurchase && vm.ChickCostPerBird > 0)
        {
            await conn.ExecuteAsync(
                @"INSERT INTO Transactions (TxnDate, TxnType, Category, Description, Amount, PaymentStatus, FarmId, Reference, BatchId)
                  VALUES (@date, 'Purchase', 'Day-old Chicks', @desc, @amount, 'Paid', @FarmId, @BatchCode, @id)",
                new
                {
                    date = vm.PlacementDate,
                    desc = $"{vm.InitialCount:N0} day-old {vm.Breed} chicks @ ৳{vm.ChickCostPerBird:0.##}" +
                           (string.IsNullOrWhiteSpace(vm.HatcheryName) ? "" : $" from {vm.HatcheryName}"),
                    amount = -(vm.InitialCount * vm.ChickCostPerBird),
                    vm.FarmId, vm.BatchCode, id
                }, tx);
        }

        var scheduled = 0;
        if (vm.ApplyVaccineProgramme && vm.VaccineTemplateId is > 0)
        {
            scheduled = await conn.ExecuteScalarAsync<int?>(
                "dbo.sp_ApplyVaccineTemplate",
                new { BatchId = id, TemplateId = vm.VaccineTemplateId.Value }, tx,
                commandType: CommandType.StoredProcedure) ?? 0;
        }

        await RecalcFarmStockAsync(conn, tx, vm.FarmId);
        tx.Commit();
        return (id, scheduled);
    }

    public async Task CloseAsync(BatchCloseVm vm)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var farmId = await conn.ExecuteScalarAsync<int>("SELECT FarmId FROM Batches WHERE Id=@BatchId", new { vm.BatchId }, tx);
        await conn.ExecuteAsync(
            @"UPDATE Batches SET
                ClosedDate      = @HarvestDate,
                IsActive        = 0,
                HealthStatus    = 'Harvested',
                HarvestedCount  = @HarvestedCount,
                HarvestWeightKg = @harvestWeight,
                AverageWeightKg = @AvgWeightKg,
                Notes = CASE WHEN @Notes IS NULL OR @Notes = '' THEN Notes
                             ELSE LEFT(CONCAT(ISNULL(Notes + ' | ', ''), 'Harvest: ', @Notes), 500) END
              WHERE Id = @BatchId",
            new
            {
                vm.BatchId, vm.HarvestDate, vm.HarvestedCount, vm.AvgWeightKg, vm.Notes,
                harvestWeight = Math.Round(vm.HarvestedCount * vm.AvgWeightKg, 2)
            }, tx);
        await RecalcFarmStockAsync(conn, tx, farmId);
        tx.Commit();
    }

    public async Task RecalcFarmStockAsync(int farmId)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        await RecalcFarmStockAsync(conn, tx, farmId);
        tx.Commit();
    }

    private static Task RecalcFarmStockAsync(IDbConnection conn, IDbTransaction tx, int farmId)
        => conn.ExecuteAsync(
            @"UPDATE Farms SET CurrentStock =
                ISNULL((SELECT SUM(CurrentCount) FROM Batches WHERE FarmId = @farmId AND IsActive = 1), 0)
              WHERE Id = @farmId", new { farmId }, tx);

    public async Task<List<BreedStandard>> GetBreedStandardAsync(string breed, string batchType)
    {
        using var conn = db.Create();
        var rows = (await conn.QueryAsync<BreedStandard>(
            "SELECT * FROM BreedStandards WHERE Breed = @breed ORDER BY AgeDay", new { breed })).ToList();
        if (rows.Count > 0) return rows;
        // fall back to the reference curve for the production type
        var fallback = batchType switch { "Sonali" => "Sonali", "Broiler" => "Cobb 500", _ => null };
        if (fallback == null) return rows;
        return (await conn.QueryAsync<BreedStandard>(
            "SELECT * FROM BreedStandards WHERE Breed = @fallback ORDER BY AgeDay", new { fallback })).ToList();
    }

    public async Task<List<DailyFarmRecord>> GetDailyRecordsAsync(int batchId)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<DailyFarmRecord>(
            "SELECT * FROM vw_DailyRecordSummary WHERE BatchId = @batchId ORDER BY RecordDate, Id", new { batchId });
        return rows.ToList();
    }

    public async Task<List<FarmVisit>> GetVisitsAsync(int batchId)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<FarmVisit>(
            "SELECT * FROM vw_FarmVisitDetails WHERE BatchId = @batchId ORDER BY VisitDate DESC", new { batchId });
        return rows.ToList();
    }

    public async Task<List<BatchOrderRow>> GetOrdersAsync(int batchId)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<BatchOrderRow>(
            @"SELECT o.Id, o.OrderCode, c.Name AS CustomerName, o.Product, o.Quantity, o.Unit, o.BirdCount,
                     o.UnitPrice, o.TotalAmount, o.OrderDate, o.DeliveryDate, o.Status, o.PaymentStatus
              FROM Orders o INNER JOIN Customers c ON o.CustomerId = c.Id
              WHERE o.BatchId = @batchId ORDER BY o.OrderDate DESC", new { batchId });
        return rows.ToList();
    }

    public async Task<List<MortalityLog>> GetMortalityLogsAsync(int batchId)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<MortalityLog>(
            @"SELECT ml.*, b.BatchCode, b.FarmId, f.Name AS FarmName
              FROM MortalityLogs ml INNER JOIN Batches b ON ml.BatchId = b.Id INNER JOIN Farms f ON b.FarmId = f.Id
              WHERE ml.BatchId = @batchId ORDER BY ml.LogDate DESC", new { batchId });
        return rows.ToList();
    }
}
