using Dapper;
using PoultryPro.Models.Entities;
using PoultryPro.Infrastructure;
using PoultryPro.Models.ViewModels;



public class VisitService(IDbConnectionFactory db, ISettingsService settings) : IVisitService
{
    /// <summary>Geofence radius (m) from AppSettings <c>Visit.GeofenceRadiusM</c> (default 300).</summary>
    public Task<decimal> GetGeofenceRadiusAsync() => settings.GetDecimalAsync(VisitTrust.SettingKey, VisitTrust.DefaultRadiusM);

    public async Task<IEnumerable<FarmVisit>> GetAllAsync(int? farmId = null, int? officerId = null)
    {
        using var conn = db.Create();
        var where = new List<string>();
        if (farmId.HasValue) where.Add("FarmId=@farmId");
        if (officerId.HasValue) where.Add("FieldOfficerId=@officerId");
        var sql = "SELECT * FROM vw_FarmVisitDetails"
                  + (where.Any() ? " WHERE " + string.Join(" AND ", where) : "")
                  + " ORDER BY VisitDate DESC";
        var visits = await conn.QueryAsync<FarmVisit>(sql, new { farmId, officerId });
        return VisitTrust.Apply(visits, await GetGeofenceRadiusAsync());
    }

    public async Task<FarmVisit?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        var visit = await conn.QuerySingleOrDefaultAsync<FarmVisit>(
            "SELECT * FROM vw_FarmVisitDetails WHERE Id=@id", new { id });
        if (visit != null) visit.Trust = VisitTrust.Evaluate(visit, await GetGeofenceRadiusAsync());
        return visit;
    }

    public async Task<int> CreateAsync(VisitCreateVm vm)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO FarmVisits
                (FieldOfficerId,FarmId,BatchId,VisitDate,VisitPurpose,VisitStatus,
                 CheckInLatitude,CheckInLongitude,GpsAccuracyMeters,OverallRemark)
              VALUES
                (@FieldOfficerId,@FarmId,@BatchId,@VisitDate,@VisitPurpose,@VisitStatus,
                 @CheckInLatitude,@CheckInLongitude,@GpsAccuracyMeters,@OverallRemark);
              SELECT SCOPE_IDENTITY();", vm);
    }

    public async Task UpdateStatusAsync(int id, string status, DateTime? checkIn = null,
                                        decimal? lat = null, decimal? lng = null,
                                        decimal? accuracy = null, string? remark = null)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE FarmVisits SET
                VisitStatus=@status,
                CheckInTime        = CASE WHEN @checkIn  IS NOT NULL THEN @checkIn  ELSE CheckInTime END,
                CheckInLatitude    = CASE WHEN @lat      IS NOT NULL THEN @lat      ELSE CheckInLatitude END,
                CheckInLongitude   = CASE WHEN @lng      IS NOT NULL THEN @lng      ELSE CheckInLongitude END,
                GpsAccuracyMeters  = CASE WHEN @accuracy IS NOT NULL THEN @accuracy ELSE GpsAccuracyMeters END,
                OverallRemark      = CASE WHEN @remark   IS NOT NULL THEN @remark   ELSE OverallRemark END,
                CheckOutTime       = CASE WHEN @status   = 'Completed' THEN GETDATE() ELSE CheckOutTime END
              WHERE Id=@id",
            new { id, status, checkIn, lat, lng, accuracy, remark });
    }

    public async Task UpdateDetailsAsync(int id, VisitCreateVm vm)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE FarmVisits SET
                FieldOfficerId=@FieldOfficerId, FarmId=@FarmId, BatchId=@BatchId,
                VisitDate=@VisitDate, VisitPurpose=@VisitPurpose
              WHERE Id=@id",
            new { id, vm.FieldOfficerId, vm.FarmId, vm.BatchId, vm.VisitDate, vm.VisitPurpose });
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("DELETE FROM FarmVisits WHERE Id=@id", new { id });
    }

    public async Task<VisitDetailVm> GetDetailAsync(int id)
    {
        using var conn = db.Create();
        var visit = await GetByIdAsync(id);
        var records = await conn.QueryAsync<DailyFarmRecord>(
            "SELECT * FROM vw_DailyRecordSummary WHERE VisitId=@id ORDER BY RecordDate DESC", new { id });
        var photos = await GetPhotosAsync(id);
        var sig = await GetSignatureAsync(id);
        Batch? batch = null;
        if (visit?.BatchId.HasValue == true)
            batch = await conn.QuerySingleOrDefaultAsync<Batch>(
                "SELECT * FROM vw_BatchDetails WHERE Id=@bid", new { bid = visit.BatchId });

        return new VisitDetailVm
        {
            Visit = visit ?? new(),
            DailyRecords = records.ToList(),
            Photos = photos.ToList(),
            Signature = sig,
            Batch = batch
        };
    }

    public async Task MarkSyncedAsync(int visitId)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE FarmVisits       SET SyncStatus='Synced',SyncedAt=GETDATE() WHERE Id=@visitId;
              UPDATE DailyFarmRecords SET SyncStatus='Synced',SyncedAt=GETDATE() WHERE VisitId=@visitId;
              UPDATE VisitPhotos      SET SyncStatus='Synced',SyncedAt=GETDATE() WHERE VisitId=@visitId;
              UPDATE FarmerSignatures SET SyncStatus='Synced',SyncedAt=GETDATE() WHERE VisitId=@visitId;",
            new { visitId });
    }

    public async Task<int> AddPhotoAsync(int visitId, string photoType, string fileName,
                                          string filePath, int? sizeKb, string? caption)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO VisitPhotos(VisitId,PhotoType,FileName,FilePath,FileSizeKb,Caption,CapturedAt)
              VALUES(@visitId,@photoType,@fileName,@filePath,@sizeKb,@caption,GETDATE());
              SELECT SCOPE_IDENTITY();",
            new { visitId, photoType, fileName, filePath, sizeKb, caption });
    }

    public async Task DeletePhotoAsync(int photoId)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("DELETE FROM VisitPhotos WHERE Id=@photoId", new { photoId });
    }

    public async Task<IEnumerable<VisitPhoto>> GetPhotosAsync(int visitId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<VisitPhoto>(
            "SELECT * FROM VisitPhotos WHERE VisitId=@visitId ORDER BY CapturedAt", new { visitId });
    }

    public async Task<int> SaveSignatureAsync(int visitId, string farmerName,
                                               string? phone, string filePath)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"IF EXISTS(SELECT 1 FROM FarmerSignatures WHERE VisitId=@visitId)
                  UPDATE FarmerSignatures SET FarmerName=@farmerName,FarmerPhone=@phone,
                         SignatureFilePath=@filePath,IsVerifiedByFarmer=1,SignedAt=GETDATE()
                  WHERE VisitId=@visitId;
              ELSE
                  INSERT INTO FarmerSignatures(VisitId,FarmerName,FarmerPhone,SignatureFilePath,IsVerifiedByFarmer,SignedAt)
                  VALUES(@visitId,@farmerName,@phone,@filePath,1,GETDATE());
              SELECT ISNULL((SELECT Id FROM FarmerSignatures WHERE VisitId=@visitId),0);",
            new { visitId, farmerName, phone, filePath });
    }

    public async Task<FarmerSignature?> GetSignatureAsync(int visitId)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<FarmerSignature>(
            "SELECT * FROM FarmerSignatures WHERE VisitId=@visitId", new { visitId });
    }
}