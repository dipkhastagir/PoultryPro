using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;


public interface IDailyRecordService
{
    Task<IEnumerable<DailyFarmRecord>> GetByVisitAsync(int visitId);
    Task<IEnumerable<DailyFarmRecord>> GetByBatchAsync(int batchId);
    Task<IEnumerable<DailyFarmRecord>> GetAll();
    Task<IEnumerable<DailyFarmRecord>> GetByFarmAsync(int farmId, DateTime? from = null, DateTime? to = null);
    Task<DailyFarmRecord?> GetByIdAsync(int id);
    Task<int> CreateAsync(DailyRecordCreateVm vm);
    Task UpdateAsync(DailyFarmRecord record);
    Task DeleteAsync(int id);
}