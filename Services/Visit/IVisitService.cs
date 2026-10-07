using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

public interface IVisitService
{
    Task<IEnumerable<FarmVisit>> GetAllAsync(int? farmId = null, int? officerId = null);
    Task<FarmVisit?> GetByIdAsync(int id);
    /// <summary>Geofence radius in metres (AppSettings Visit.GeofenceRadiusM).</summary>
    Task<decimal> GetGeofenceRadiusAsync();
    Task<int> CreateAsync(VisitCreateVm vm);
    Task UpdateStatusAsync(int id, string status, DateTime? checkIn = null,
                           decimal? lat = null, decimal? lng = null,
                           decimal? accuracy = null, string? remark = null);
    Task UpdateDetailsAsync(int id, VisitCreateVm vm);
    Task DeleteAsync(int id);
    Task<VisitDetailVm> GetDetailAsync(int id);
    Task MarkSyncedAsync(int visitId);

    // Photos
    Task<int> AddPhotoAsync(int visitId, string photoType, string fileName,
                            string filePath, int? sizeKb, string? caption);
    Task DeletePhotoAsync(int photoId);
    Task<IEnumerable<VisitPhoto>> GetPhotosAsync(int visitId);

    // Signature
    Task<int> SaveSignatureAsync(int visitId, string farmerName, string? phone, string filePath);
    Task<FarmerSignature?> GetSignatureAsync(int visitId);
}