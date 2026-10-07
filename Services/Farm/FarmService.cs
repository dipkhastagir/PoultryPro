using Dapper;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;


public class FarmService(IDbConnectionFactory db, ISensorService sensors, ISettingsService settings) : IFarmService
{
    public async Task<IEnumerable<Farm>> GetAllAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Farm>("SELECT * FROM Farms WHERE IsActive=1 ORDER BY Name");
    }
    public async Task<Farm?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<Farm>("SELECT * FROM Farms WHERE Id=@id", new { id });
    }
    public async Task<int> CreateAsync(Farm farm)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Farms(Name,Location,Capacity,CurrentStock,EmployeeCount,CertStatus,CertExpiry,
                                FarmType,Latitude,Longitude,OwnerName,OwnerPhone,Zone,ShedCount,AreaSqFt,Notes)
              VALUES(@Name,@Location,@Capacity,@CurrentStock,@EmployeeCount,@CertStatus,@CertExpiry,
                     @FarmType,@Latitude,@Longitude,@OwnerName,@OwnerPhone,@Zone,@ShedCount,@AreaSqFt,@Notes);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", Normalize(farm));
    }
    public async Task UpdateAsync(Farm farm)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE Farms SET Name=@Name,Location=@Location,Capacity=@Capacity,
              CurrentStock=@CurrentStock,EmployeeCount=@EmployeeCount,
              CertStatus=@CertStatus,CertExpiry=@CertExpiry,
              FarmType=@FarmType,Latitude=@Latitude,Longitude=@Longitude,OwnerName=@OwnerName,OwnerPhone=@OwnerPhone,
              Zone=@Zone,ShedCount=@ShedCount,AreaSqFt=@AreaSqFt,Notes=@Notes
              WHERE Id=@Id", Normalize(farm));
    }

    private static Farm Normalize(Farm f)
    {
        f.FarmType = string.IsNullOrWhiteSpace(f.FarmType) ? "Broiler" : f.FarmType.Trim();
        f.CertStatus = string.IsNullOrWhiteSpace(f.CertStatus) ? "Valid" : f.CertStatus;
        if (f.ShedCount < 1) f.ShedCount = 1;
        if (f.Latitude.HasValue) f.Latitude = Math.Round(f.Latitude.Value, 6);
        if (f.Longitude.HasValue) f.Longitude = Math.Round(f.Longitude.Value, 6);
        return f;
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("UPDATE Farms SET IsActive=0 WHERE Id=@id", new { id });
    }
    public async Task<IEnumerable<Certification>> GetCertificationsAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Certification>(
            @"SELECT c.*,f.Name AS FarmName FROM Certifications c
              INNER JOIN Farms f ON c.FarmId=f.Id ORDER BY c.ExpiryDate");
    }

    // ── v2 ──────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<FarmBenchmarkRow>> GetBenchmarksAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<FarmBenchmarkRow>("SELECT * FROM vw_FarmBenchmark ORDER BY FarmName");
    }

    public async Task<IEnumerable<Certification>> GetCertificationsByFarmAsync(int farmId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Certification>(
            @"SELECT c.*, f.Name AS FarmName FROM Certifications c INNER JOIN Farms f ON f.Id = c.FarmId
              WHERE c.FarmId = @farmId ORDER BY c.ExpiryDate DESC", new { farmId });
    }

    public async Task<FarmDetailVm?> GetDetailAsync(int id)
    {
        using var conn = db.Create();
        var farm = await conn.QuerySingleOrDefaultAsync<Farm>("SELECT * FROM Farms WHERE Id=@id", new { id });
        if (farm == null) return null;

        var vm = new FarmDetailVm { Farm = farm };
        vm.Benchmark = await conn.QuerySingleOrDefaultAsync<FarmBenchmarkRow>("SELECT * FROM vw_FarmBenchmark WHERE FarmId=@id", new { id });

        var batches = (await conn.QueryAsync<Batch>(
            "SELECT * FROM vw_BatchPerformance WHERE FarmId=@id ORDER BY IsActive DESC, PlacementDate DESC", new { id })).ToList();
        vm.ActiveBatches = batches.Where(b => b.IsActive).ToList();
        vm.PastBatches = batches.Where(b => !b.IsActive).Take(12).ToList();

        vm.Certifications = (await GetCertificationsByFarmAsync(id)).ToList();
        vm.LatestAudit = await conn.QuerySingleOrDefaultAsync<FarmAuditSummary>(
            "SELECT TOP 1 Id, AuditDate, AuditorName, Score, MaxScore, ScorePct, Grade, Notes FROM BiosecurityAudits WHERE FarmId=@id ORDER BY AuditDate DESC, Id DESC",
            new { id });

        vm.RecentVisits = (await conn.QueryAsync<FarmVisit>(
            "SELECT TOP 10 * FROM vw_FarmVisitDetails WHERE FarmId=@id ORDER BY VisitDate DESC, Id DESC", new { id })).ToList();

        vm.Officers = (await conn.QueryAsync<FarmOfficerRow>(
            @"SELECT o.Id, o.FullName, o.Phone, o.Zone, o.Designation, a.AssignedDate,
                     (SELECT COUNT(*) FROM FarmVisits v WHERE v.FieldOfficerId = o.Id AND v.FarmId = @id) AS VisitCount,
                     (SELECT MAX(v.VisitDate) FROM FarmVisits v WHERE v.FieldOfficerId = o.Id AND v.FarmId = @id) AS LastVisit
              FROM FieldOfficerFarmAssignments a INNER JOIN FieldOfficers o ON o.Id = a.FieldOfficerId
              WHERE a.FarmId = @id AND a.IsActive = 1 AND o.IsActive = 1
              ORDER BY o.FullName", new { id })).ToList();

        vm.Employees = (await conn.QueryAsync<Employee>(
            "SELECT e.*, f.Name AS FarmName FROM Employees e LEFT JOIN Farms f ON f.Id = e.FarmId WHERE e.FarmId=@id AND e.IsActive=1 ORDER BY e.FullName",
            new { id })).ToList();

        vm.OpenAlerts = (await conn.QueryAsync<Alert>(
            @"SELECT TOP 8 a.*, f.Name AS FarmName, b.BatchCode FROM Alerts a
              LEFT JOIN Farms f ON f.Id = a.FarmId LEFT JOIN Batches b ON b.Id = a.BatchId
              WHERE a.IsResolved = 0 AND a.FarmId = @id
              ORDER BY CASE a.Severity WHEN 'Critical' THEN 0 WHEN 'Warning' THEN 1 ELSE 2 END, a.CreatedAt DESC", new { id })).ToList();

        vm.Sensors = await sensors.GetDeviceCardsAsync(id);
        vm.ThiWarning = await settings.GetDecimalAsync("Alert.THIWarning", 80m);
        vm.ThiCritical = await settings.GetDecimalAsync("Alert.THICritical", 85m);
        return vm;
    }

    public async Task<FarmMapVm> GetMapAsync(int checkIns = 100)
    {
        using var conn = db.Create();
        var points = (await conn.QueryAsync<FarmMapPoint>(
            @"SELECT f.Id, f.Name, f.Location, f.FarmType, f.Zone, f.Latitude AS Lat, f.Longitude AS Lng, f.Capacity,
                     ISNULL(bm.CurrentBirds, 0) AS CurrentBirds, ISNULL(bm.ActiveBatches, 0) AS ActiveBatches,
                     ISNULL(bm.AvgFCR, 0) AS AvgFCR, ISNULL(bm.AvgEPEF, 0) AS AvgEPEF, ISNULL(bm.AvgMortalityRate, 0) AS AvgMortalityRate,
                     bm.LastBiosecurityPct AS BiosecurityPct,
                     CAST(CASE WHEN f.Capacity > 0 THEN ISNULL(bm.CurrentBirds, 0) * 100.0 / f.Capacity ELSE 0 END AS DECIMAL(10,1)) AS UtilizationPct,
                     (SELECT COUNT(*) FROM Alerts a WHERE a.FarmId = f.Id AND a.IsResolved = 0 AND a.Severity = 'Critical') AS CriticalAlerts,
                     (SELECT COUNT(*) FROM Alerts a WHERE a.FarmId = f.Id AND a.IsResolved = 0 AND a.Severity = 'Warning')  AS WarningAlerts,
                     (SELECT COUNT(*) FROM Alerts a WHERE a.FarmId = f.Id AND a.IsResolved = 0 AND a.Severity = 'Info')     AS InfoAlerts
              FROM Farms f
              LEFT JOIN vw_FarmBenchmark bm ON bm.FarmId = f.Id
              WHERE f.IsActive = 1 AND f.Latitude IS NOT NULL AND f.Longitude IS NOT NULL
              ORDER BY f.Name")).ToList();

        var missing = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Farms WHERE IsActive = 1 AND (Latitude IS NULL OR Longitude IS NULL)");

        var visits = (await conn.QueryAsync<VisitCheckInPoint>(
            @"SELECT TOP (@n) Id, FarmName, FieldOfficerName, VisitDate, VisitPurpose, VisitStatus,
                     CheckInLatitude AS Lat, CheckInLongitude AS Lng, GpsAccuracyMeters
              FROM vw_FarmVisitDetails
              WHERE CheckInLatitude IS NOT NULL AND CheckInLongitude IS NOT NULL
              ORDER BY ISNULL(CheckInTime, VisitDate) DESC, Id DESC", new { n = checkIns })).ToList();

        return new FarmMapVm { Farms = points, CheckIns = visits, FarmsWithoutGps = missing };
    }

    public async Task<int> AddCertificationAsync(Certification cert)
    {
        using var conn = db.Create();
        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Certifications (FarmId, CertName, IssuedDate, ExpiryDate, Status, IssuingAuthority, CertificateNo)
              VALUES (@FarmId, @CertName, @IssuedDate, @ExpiryDate, @Status, @IssuingAuthority, @CertificateNo);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new
            {
                cert.FarmId, CertName = cert.CertName.Trim(), cert.IssuedDate, cert.ExpiryDate,
                Status = cert.ExpiryDate.Date < DateTime.Today ? "Expired" : cert.ExpiryDate.Date <= DateTime.Today.AddDays(30) ? "Expiring Soon" : "Valid",
                cert.IssuingAuthority, cert.CertificateNo
            });
        await SyncFarmCertStatusAsync(conn, cert.FarmId);
        return id;
    }

    public async Task<int?> DeleteCertificationAsync(int id)
    {
        using var conn = db.Create();
        var farmId = await conn.ExecuteScalarAsync<int?>("SELECT FarmId FROM Certifications WHERE Id=@id", new { id });
        if (farmId == null) return null;
        await conn.ExecuteAsync("DELETE FROM Certifications WHERE Id=@id", new { id });
        await SyncFarmCertStatusAsync(conn, farmId.Value);
        return farmId;
    }

    /// <summary>Keeps the farm's headline certification status in line with its certificates (earliest unexpired expiry).</summary>
    private static async Task SyncFarmCertStatusAsync(System.Data.IDbConnection conn, int farmId)
    {
        await conn.ExecuteAsync(
            @"DECLARE @next DATE = (SELECT MIN(ExpiryDate) FROM Certifications WHERE FarmId=@farmId AND ExpiryDate >= CAST(GETDATE() AS DATE));
              DECLARE @any INT = (SELECT COUNT(*) FROM Certifications WHERE FarmId=@farmId);
              IF @any > 0
                  UPDATE Farms SET CertExpiry = ISNULL(@next, (SELECT MAX(ExpiryDate) FROM Certifications WHERE FarmId=@farmId)),
                                   CertStatus = CASE WHEN @next IS NULL THEN 'Expired'
                                                     WHEN @next <= DATEADD(DAY, 30, CAST(GETDATE() AS DATE)) THEN 'Expiring Soon'
                                                     ELSE 'Valid' END
                  WHERE Id=@farmId;", new { farmId });
    }
}
