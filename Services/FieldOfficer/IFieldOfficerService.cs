using PoultryPro.Models.DTO;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;


public interface IFieldOfficerService
{
    Task<IEnumerable<FieldOfficer>> GetAllAsync();
    Task<FieldOfficer?> GetByIdAsync(int id);
    Task<int> CreateAsync(FieldOfficerCreateVm vm);
    Task UpdateAsync(FieldOfficerEditVm vm);
    Task DeleteAsync(int id);
    Task ToggleActiveAsync(int id);
    Task<IEnumerable<FieldOfficerFarmAssignment>> GetAssignmentsAsync(int fieldOfficerId);
    Task AssignFarmsAsync(int fieldOfficerId, List<int> farmIds);
    Task<IEnumerable<FarmVisit>> GetVisitsAsync(int? fieldOfficerId = null);
    Task<IEnumerable<DailyFarmRecord>> GetDailyRecordsAsync(int? fieldOfficerId = null);
    Task<IEnumerable<PendingSyncSummary>> GetPendingSyncSummaryAsync();
    Task MarkVisitSyncedAsync(int visitId);
}