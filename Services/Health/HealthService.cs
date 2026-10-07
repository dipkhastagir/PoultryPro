using Dapper;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

public class HealthService(IDbConnectionFactory db) : IHealthService
{
    private const string VaxSelect =
        @"SELECT v.*, b.BatchCode, b.Breed, b.FarmId, f.Name AS FarmName
          FROM VaccinationRecords v
          INNER JOIN Batches b ON v.BatchId = b.Id
          INNER JOIN Farms f ON b.FarmId = f.Id";

    private const string CaseSelect =
        @"SELECT dc.*, b.BatchCode, b.FarmId, f.Name AS FarmName
          FROM DiseaseCases dc
          INNER JOIN Batches b ON dc.BatchId = b.Id
          INNER JOIN Farms f ON b.FarmId = f.Id";

    public async Task<IEnumerable<VaccinationRecord>> GetVaccinationsAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<VaccinationRecord>(VaxSelect + " ORDER BY v.DueDate");
    }

    public async Task<int> CreateVaccinationAsync(VaccinationRecord record)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO VaccinationRecords(BatchId,VaccineName,DueDate,CompletedDate,Status,DoseMl,Route,AdministeredBy,Notes)
              VALUES(@BatchId,@VaccineName,@DueDate,@CompletedDate,@Status,@DoseMl,@Route,@AdministeredBy,@Notes);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", record);
    }

    public Task MarkVaccinationDoneAsync(int id) => MarkVaccinationDoneAsync(id, null);

    public async Task MarkVaccinationDoneAsync(int id, string? administeredBy)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE VaccinationRecords SET Status='Done', CompletedDate=CAST(GETDATE() AS DATE),
                     AdministeredBy = ISNULL(@administeredBy, AdministeredBy)
              WHERE Id=@id", new { id, administeredBy });
    }

    public async Task<VaccinationRecord?> GetVaccinationAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<VaccinationRecord>(VaxSelect + " WHERE v.Id = @id", new { id });
    }

    public async Task UpdateVaccinationAsync(VaccinationRecord r)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE VaccinationRecords SET VaccineName=@VaccineName, DueDate=@DueDate, CompletedDate=@CompletedDate,
                     Status=@Status, DoseMl=@DoseMl, Route=@Route, AdministeredBy=@AdministeredBy, Notes=@Notes
              WHERE Id=@Id", r);
    }

    public async Task<IEnumerable<DiseaseCase>> GetDiseaseCasesAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<DiseaseCase>(CaseSelect + " ORDER BY dc.ReportedDate DESC");
    }

    public async Task<DiseaseCase?> GetDiseaseCaseAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<DiseaseCase>(CaseSelect + " WHERE dc.Id = @id", new { id });
    }

    public async Task<int> CreateDiseaseCaseAsync(DiseaseCase dc)
    {
        using var conn = db.Create();
        dc.Treatment ??= "";
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO DiseaseCases(BatchId,DiseaseName,ReportedDate,AffectedCount,DeathCount,Treatment,Status,ResolvedDate,DiagnosedBy,Notes)
              VALUES(@BatchId,@DiseaseName,@ReportedDate,@AffectedCount,@DeathCount,@Treatment,@Status,
                     CASE WHEN @Status='Resolved' THEN ISNULL(@ResolvedDate, CAST(GETDATE() AS DATE)) ELSE NULL END,
                     @DiagnosedBy,@Notes);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", dc);
    }

    public async Task UpdateDiseaseCaseAsync(DiseaseCase dc)
    {
        using var conn = db.Create();
        dc.Treatment ??= "";
        await conn.ExecuteAsync(
            @"UPDATE DiseaseCases SET Treatment=@Treatment, Status=@Status, ResolvedDate=@ResolvedDate, Notes=@Notes,
                     AffectedCount=@AffectedCount, DeathCount=@DeathCount, DiagnosedBy=@DiagnosedBy
              WHERE Id=@Id", dc);
    }

    public async Task<List<VaccinationRecord>> GetVaccinationsByBatchAsync(int batchId)
    {
        using var conn = db.Create();
        return (await conn.QueryAsync<VaccinationRecord>(VaxSelect + " WHERE v.BatchId = @batchId ORDER BY v.DueDate", new { batchId })).ToList();
    }

    public async Task<List<DiseaseCase>> GetDiseaseCasesByBatchAsync(int batchId)
    {
        using var conn = db.Create();
        return (await conn.QueryAsync<DiseaseCase>(CaseSelect + " WHERE dc.BatchId = @batchId ORDER BY dc.ReportedDate DESC", new { batchId })).ToList();
    }

    public async Task<List<VaccinationComplianceRow>> GetComplianceByBatchAsync(bool activeOnly = true)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<VaccinationComplianceRow>(
            @"SELECT b.Id AS BatchId, b.BatchCode, b.FarmId, f.Name AS FarmName,
                     SUM(CASE WHEN v.DueDate <= CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS DueToDate,
                     SUM(CASE WHEN v.DueDate <= CAST(GETDATE() AS DATE) AND v.CompletedDate IS NOT NULL THEN 1 ELSE 0 END) AS Done,
                     SUM(CASE WHEN v.CompletedDate IS NOT NULL AND v.CompletedDate <= DATEADD(DAY, 1, v.DueDate) THEN 1 ELSE 0 END) AS OnTime,
                     SUM(CASE WHEN v.CompletedDate IS NULL AND v.DueDate < CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS Overdue,
                     SUM(CASE WHEN v.CompletedDate IS NULL AND v.DueDate >= CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS Upcoming
              FROM Batches b
              INNER JOIN Farms f ON b.FarmId = f.Id
              INNER JOIN VaccinationRecords v ON v.BatchId = b.Id
              WHERE (@activeOnly = 0 OR b.IsActive = 1)
              GROUP BY b.Id, b.BatchCode, b.FarmId, f.Name
              ORDER BY f.Name, b.BatchCode", new { activeOnly });
        return rows.ToList();
    }

    public async Task<HealthReportVm> GetHealthReportAsync(int months = 12)
    {
        using var conn = db.Create();
        var vm = new HealthReportVm();

        var perBatch = await GetComplianceByBatchAsync(false);
        vm.FarmCompliance = perBatch.GroupBy(r => new { r.FarmId, r.FarmName })
            .Select(g => new VaccinationComplianceRow
            {
                FarmId = g.Key.FarmId, FarmName = g.Key.FarmName, BatchCode = $"{g.Count()} flocks",
                DueToDate = g.Sum(x => x.DueToDate), Done = g.Sum(x => x.Done), OnTime = g.Sum(x => x.OnTime),
                Overdue = g.Sum(x => x.Overdue), Upcoming = g.Sum(x => x.Upcoming)
            })
            .OrderBy(r => r.CompliancePct).ToList();
        var due = vm.FarmCompliance.Sum(r => r.DueToDate);
        vm.OverallCompliance = due > 0 ? Math.Round((decimal)vm.FarmCompliance.Sum(r => r.Done) / due * 100, 1) : 100;

        vm.DiseaseByName = (await conn.QueryAsync<HealthDiseaseRow>(
            @"SELECT DiseaseName, COUNT(*) AS Cases, SUM(AffectedCount) AS Affected, SUM(DeathCount) AS Deaths,
                     SUM(CASE WHEN Status <> 'Resolved' THEN 1 ELSE 0 END) AS Active
              FROM DiseaseCases GROUP BY DiseaseName ORDER BY COUNT(*) DESC, SUM(AffectedCount) DESC")).ToList();

        vm.DiseaseByFarm = (await conn.QueryAsync<HealthDiseaseFarmRow>(
            @"SELECT f.Name AS FarmName, dc.DiseaseName, COUNT(*) AS Cases, SUM(dc.AffectedCount) AS Affected, SUM(dc.DeathCount) AS Deaths
              FROM DiseaseCases dc INNER JOIN Batches b ON dc.BatchId = b.Id INNER JOIN Farms f ON b.FarmId = f.Id
              GROUP BY f.Name, dc.DiseaseName ORDER BY f.Name, COUNT(*) DESC")).ToList();

        vm.MortalityCauses = (await conn.QueryAsync<HealthCauseRow>(
            @"SELECT ISNULL(NULLIF(LTRIM(RTRIM(Cause)), ''), 'Unknown') AS Cause, SUM([Count]) AS Birds, COUNT(*) AS Events
              FROM MortalityLogs GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(Cause)), ''), 'Unknown')
              ORDER BY SUM([Count]) DESC")).ToList();

        var from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-(months - 1));
        var monthly = (await conn.QueryAsync<HealthMonthRow>(
            @"SELECT YEAR(RecordDate) AS [Year], MONTH(RecordDate) AS [Month],
                     SUM(MortalityCount) AS Deaths, SUM(CullingCount) AS Culls, SUM(CAST(BirdCount AS BIGINT)) AS BirdDays
              FROM DailyFarmRecords WHERE RecordDate >= @from
              GROUP BY YEAR(RecordDate), MONTH(RecordDate)", new { from })).ToList();
        for (var m = from; m <= DateTime.Today; m = m.AddMonths(1))
        {
            var row = monthly.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)
                      ?? new HealthMonthRow { Year = m.Year, Month = m.Month };
            vm.MonthlyMortality.Add(row);
        }

        vm.TotalCases = vm.DiseaseByName.Sum(d => d.Cases);
        vm.ActiveCases = vm.DiseaseByName.Sum(d => d.Active);
        vm.TotalDeathsLogged = vm.MortalityCauses.Sum(c => c.Birds);
        return vm;
    }
}
