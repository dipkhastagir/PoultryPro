using Dapper;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

/// <summary>
/// Data access for the field-officer mobile app API (api/mobile/*): officer lookup, assigned farms with
/// active flocks, GPS check-in visits and visit completion.
/// </summary>
public interface IMobileSyncService
{
    Task<FieldOfficer?> GetOfficerByUsernameAsync(string username);
    Task<FieldOfficer?> GetOfficerAsync(int id);
    Task TouchLoginAsync(int officerId);
    Task<List<MobileFarmDto>> GetAssignedFarmsAsync(int officerId);
    Task<bool> IsFarmAssignedAsync(int officerId, int farmId);
    Task<(int Id, int FarmId, int? BatchId, int FieldOfficerId, string VisitStatus)?> GetVisitAsync(int visitId);
    Task<(int Id, int FarmId, bool IsActive)?> GetBatchAsync(int batchId);
    Task<bool> FarmExistsAsync(int farmId);
    Task<int> CreateVisitAsync(MobileVisitRequest r);
    Task CompleteVisitAsync(int visitId, string? remark);
}

public class MobileSyncService(IDbConnectionFactory db) : IMobileSyncService
{
    public async Task<FieldOfficer?> GetOfficerByUsernameAsync(string username)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<FieldOfficer>(
            "SELECT * FROM FieldOfficers WHERE Username = @username", new { username });
    }

    public async Task<FieldOfficer?> GetOfficerAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<FieldOfficer>("SELECT * FROM FieldOfficers WHERE Id = @id", new { id });
    }

    public async Task TouchLoginAsync(int officerId)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("UPDATE FieldOfficers SET LastLoginAt = GETDATE() WHERE Id = @officerId", new { officerId });
    }

    public async Task<List<MobileFarmDto>> GetAssignedFarmsAsync(int officerId)
    {
        using var conn = db.Create();
        var farms = (await conn.QueryAsync<MobileFarmDto>(
            @"SELECT DISTINCT f.Id AS FarmId, f.Name AS FarmName, f.Location, f.FarmType, f.Zone, f.Latitude, f.Longitude, f.OwnerName, f.OwnerPhone
              FROM FieldOfficerFarmAssignments a
              INNER JOIN Farms f ON f.Id = a.FarmId
              WHERE a.FieldOfficerId = @officerId AND a.IsActive = 1 AND f.IsActive = 1
              ORDER BY f.Name", new { officerId })).ToList();
        if (farms.Count == 0) return farms;

        var batches = await conn.QueryAsync<MobileBatchDto>(
            @"SELECT Id AS BatchId, FarmId, BatchCode, Breed, BatchType, ShedNo, AgeInDays, CurrentCount, AverageWeightKg, HealthStatus
              FROM vw_BatchDetails WHERE IsActive = 1 AND FarmId IN @ids ORDER BY PlacementDate DESC",
            new { ids = farms.Select(f => f.FarmId).ToArray() });
        var byFarm = batches.GroupBy(b => b.FarmId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var f in farms)
            f.ActiveBatches = byFarm.TryGetValue(f.FarmId, out var list) ? list : new();
        return farms;
    }

    public async Task<bool> IsFarmAssignedAsync(int officerId, int farmId)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM FieldOfficerFarmAssignments WHERE FieldOfficerId=@officerId AND FarmId=@farmId AND IsActive=1",
            new { officerId, farmId }) > 0;
    }

    public async Task<(int Id, int FarmId, int? BatchId, int FieldOfficerId, string VisitStatus)?> GetVisitAsync(int visitId)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<(int, int, int?, int, string)>(
            "SELECT Id, FarmId, BatchId, FieldOfficerId, VisitStatus FROM FarmVisits WHERE Id = @visitId", new { visitId });
        return rows.Any() ? rows.First() : null;
    }

    public async Task<(int Id, int FarmId, bool IsActive)?> GetBatchAsync(int batchId)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<(int, int, bool)>(
            "SELECT Id, FarmId, IsActive FROM Batches WHERE Id = @batchId", new { batchId });
        return rows.Any() ? rows.First() : null;
    }

    public async Task<bool> FarmExistsAsync(int farmId)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Farms WHERE Id=@farmId AND IsActive=1", new { farmId }) > 0;
    }

    public async Task<int> CreateVisitAsync(MobileVisitRequest r)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO FarmVisits (FieldOfficerId, FarmId, BatchId, VisitDate, VisitPurpose, VisitStatus, CheckInTime,
                                      CheckInLatitude, CheckInLongitude, GpsAccuracyMeters, OverallRemark, SyncStatus)
              VALUES (@FieldOfficerId, @FarmId, @BatchId, @VisitDate, @VisitPurpose, 'In Progress', GETDATE(),
                      @Latitude, @Longitude, @GpsAccuracyMeters, @Remark, 'Pending');
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new
            {
                r.FieldOfficerId, r.FarmId, r.BatchId,
                VisitDate = (r.VisitDate ?? DateTime.Today).Date,
                VisitPurpose = string.IsNullOrWhiteSpace(r.VisitPurpose) ? "Routine Check" : r.VisitPurpose.Trim(),
                r.Latitude, r.Longitude, r.GpsAccuracyMeters, r.Remark
            });
    }

    public async Task CompleteVisitAsync(int visitId, string? remark)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE FarmVisits SET VisitStatus = 'Completed', CheckOutTime = GETDATE(),
                     CheckInTime = ISNULL(CheckInTime, GETDATE()),
                     OverallRemark = CASE WHEN @remark IS NULL OR @remark = '' THEN OverallRemark ELSE @remark END
              WHERE Id = @visitId", new { visitId, remark });
    }
}
