using Dapper;
using System.Data;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;
using PoultryPro.Models.DTO;


public class FieldOfficerService(IDbConnectionFactory db) : IFieldOfficerService
{
    private static string HashPassword(string password)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(password + "PoultryProSalt2025"));
        return Convert.ToHexString(bytes).ToLower();
    }

    public async Task<IEnumerable<FieldOfficer>> GetAllAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<FieldOfficer>(
            @"SELECT fo.*,
                (SELECT COUNT(*) FROM FieldOfficerFarmAssignments
                 WHERE FieldOfficerId=fo.Id AND IsActive=1)              AS AssignedFarmCount,
                (SELECT COUNT(*) FROM FarmVisits
                 WHERE FieldOfficerId=fo.Id AND SyncStatus='Pending')    AS PendingVisits
              FROM FieldOfficers fo
              ORDER BY fo.IsActive DESC, fo.FullName");
    }

    public async Task<FieldOfficer?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<FieldOfficer>(
            @"SELECT fo.*,
                (SELECT COUNT(*) FROM FieldOfficerFarmAssignments
                 WHERE FieldOfficerId=fo.Id AND IsActive=1)              AS AssignedFarmCount,
                (SELECT COUNT(*) FROM FarmVisits
                 WHERE FieldOfficerId=fo.Id AND SyncStatus='Pending')    AS PendingVisits
              FROM FieldOfficers fo WHERE fo.Id=@id", new { id });
    }

    public async Task<int> CreateAsync(FieldOfficerCreateVm vm)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO FieldOfficers(FullName,Username,PasswordHash,Phone,Email,Zone,Designation)
              VALUES(@FullName,@Username,@PasswordHash,@Phone,@Email,@Zone,@Designation);
              SELECT SCOPE_IDENTITY();",
            new
            {
                vm.FullName,
                vm.Username,
                PasswordHash = HashPassword(vm.Password),
                vm.Phone,
                vm.Email,
                vm.Zone,
                vm.Designation
            }, tx);
        if (vm.AssignedFarmIds.Any())
            await UpsertAssignmentsAsync(conn, tx, id, vm.AssignedFarmIds);
        tx.Commit();
        return id;
    }

    public async Task UpdateAsync(FieldOfficerEditVm vm)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        if (!string.IsNullOrWhiteSpace(vm.NewPassword))
            await conn.ExecuteAsync(
                @"UPDATE FieldOfficers SET FullName=@FullName,Username=@Username,
                  PasswordHash=@PasswordHash,Phone=@Phone,Email=@Email,
                  Zone=@Zone,Designation=@Designation,IsActive=@IsActive WHERE Id=@Id",
                new
                {
                    vm.FullName,
                    vm.Username,
                    PasswordHash = HashPassword(vm.NewPassword),
                    vm.Phone,
                    vm.Email,
                    vm.Zone,
                    vm.Designation,
                    vm.IsActive,
                    vm.Id
                }, tx);
        else
            await conn.ExecuteAsync(
                @"UPDATE FieldOfficers SET FullName=@FullName,Username=@Username,
                  Phone=@Phone,Email=@Email,Zone=@Zone,
                  Designation=@Designation,IsActive=@IsActive WHERE Id=@Id",
                new
                {
                    vm.FullName,
                    vm.Username,
                    vm.Phone,
                    vm.Email,
                    vm.Zone,
                    vm.Designation,
                    vm.IsActive,
                    vm.Id
                }, tx);

        await conn.ExecuteAsync(
            "UPDATE FieldOfficerFarmAssignments SET IsActive=0 WHERE FieldOfficerId=@id",
            new { id = vm.Id }, tx);
        if (vm.AssignedFarmIds.Any())
            await UpsertAssignmentsAsync(conn, tx, vm.Id, vm.AssignedFarmIds);
        tx.Commit();
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync(
            "UPDATE FieldOfficerFarmAssignments SET IsActive=0 WHERE FieldOfficerId=@id",
            new { id }, tx);
        await conn.ExecuteAsync(
            "UPDATE FieldOfficers SET IsActive=0 WHERE Id=@id",
            new { id }, tx);
        tx.Commit();
    }

    public async Task ToggleActiveAsync(int id)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            "UPDATE FieldOfficers SET IsActive=CASE WHEN IsActive=1 THEN 0 ELSE 1 END WHERE Id=@id",
            new { id });
    }

    public async Task<IEnumerable<FieldOfficerFarmAssignment>> GetAssignmentsAsync(int fieldOfficerId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<FieldOfficerFarmAssignment>(
            @"SELECT a.*,f.Name AS FarmName,f.Location AS FarmLocation
              FROM FieldOfficerFarmAssignments a
              INNER JOIN Farms f ON a.FarmId=f.Id
              WHERE a.FieldOfficerId=@fieldOfficerId AND a.IsActive=1
              ORDER BY f.Name",
            new { fieldOfficerId });
    }

    public async Task AssignFarmsAsync(int fieldOfficerId, List<int> farmIds)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync(
            "UPDATE FieldOfficerFarmAssignments SET IsActive=0 WHERE FieldOfficerId=@fieldOfficerId",
            new { fieldOfficerId }, tx);
        await UpsertAssignmentsAsync(conn, tx, fieldOfficerId, farmIds);
        tx.Commit();
    }

    public async Task<IEnumerable<FarmVisit>> GetVisitsAsync(int? fieldOfficerId = null)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<FarmVisit>(
            fieldOfficerId.HasValue
                ? "SELECT * FROM vw_FarmVisitDetails WHERE FieldOfficerId=@fieldOfficerId ORDER BY VisitDate DESC"
                : "SELECT * FROM vw_FarmVisitDetails ORDER BY VisitDate DESC",
            new { fieldOfficerId });
    }

    public async Task<IEnumerable<DailyFarmRecord>> GetDailyRecordsAsync(int? fieldOfficerId = null)
    {
        using var conn = db.Create();
        var where = fieldOfficerId.HasValue ? "WHERE v.FieldOfficerId=@fieldOfficerId" : "";
        return await conn.QueryAsync<DailyFarmRecord>(
            $@"SELECT dr.*,b.BatchCode,f.Name AS FarmName
               FROM DailyFarmRecords dr
               INNER JOIN FarmVisits v ON dr.VisitId=v.Id
               INNER JOIN Batches b ON dr.BatchId=b.Id
               INNER JOIN Farms f ON v.FarmId=f.Id
               {where}
               ORDER BY dr.RecordDate DESC",
            new { fieldOfficerId });
    }

    public async Task<IEnumerable<PendingSyncSummary>> GetPendingSyncSummaryAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<PendingSyncSummary>(
            "SELECT * FROM vw_FieldOfficerPendingSync ORDER BY FullName");
    }

    public async Task MarkVisitSyncedAsync(int visitId)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE FarmVisits SET SyncStatus='Synced',SyncedAt=GETDATE() WHERE Id=@visitId;
              UPDATE DailyFarmRecords SET SyncStatus='Synced',SyncedAt=GETDATE() WHERE VisitId=@visitId;
              UPDATE VisitPhotos SET SyncStatus='Synced',SyncedAt=GETDATE() WHERE VisitId=@visitId;
              UPDATE FarmerSignatures SET SyncStatus='Synced',SyncedAt=GETDATE() WHERE VisitId=@visitId;",
            new { visitId });
    }

    private static async Task UpsertAssignmentsAsync(
        IDbConnection conn, IDbTransaction tx, int officerId, List<int> farmIds)
    {
        foreach (var farmId in farmIds.Distinct())
            await conn.ExecuteAsync(
                @"IF EXISTS (SELECT 1 FROM FieldOfficerFarmAssignments
                             WHERE FieldOfficerId=@officerId AND FarmId=@farmId)
                      UPDATE FieldOfficerFarmAssignments SET IsActive=1,AssignedDate=GETDATE()
                      WHERE FieldOfficerId=@officerId AND FarmId=@farmId
                  ELSE
                      INSERT INTO FieldOfficerFarmAssignments(FieldOfficerId,FarmId)
                      VALUES(@officerId,@farmId);",
                new { officerId, farmId }, tx);
    }
}