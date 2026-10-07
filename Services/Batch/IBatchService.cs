using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;


public interface IBatchService
{
    Task<IEnumerable<Batch>> GetAllByFarmAsync(int farmId);
    Task<IEnumerable<Batch>> GetAllAsync();
    Task<Batch?> GetByIdAsync(int id);
    Task<int> CreateAsync(BatchCreateVm vm);
    Task UpdateAsync(Batch batch);
    Task DeleteAsync(int id);        // soft delete
    Task UpdateStatsAsync(int batchId, int currentCount, int mortalityDelta,
                          int cullingDelta, decimal avgWeightKg, decimal feedAddedKg);

    // ── Flock lifecycle (v2) ──
    /// <summary>Flock register from vw_BatchPerformance. status = active | closed | all.</summary>
    Task<List<Batch>> GetRegisterAsync(int? farmId, string status);
    /// <summary>One flock with full KPIs (EPEF, ADG, feed/bird/day) from vw_BatchPerformance.</summary>
    Task<Batch?> GetPerformanceAsync(int id);
    Task<List<Batch>> GetActiveAsync(string? batchType = null);
    Task<List<FlockHealthFlag>> GetHealthFlagsAsync();
    Task<string> SuggestNextCodeAsync();
    Task<bool> CodeExistsAsync(string code, int excludeId = 0);
    /// <summary>Creates the flock, optionally posts the chick purchase and applies a vaccine programme — all in one transaction.</summary>
    Task<(int BatchId, int VaccinesScheduled)> CreateFlockAsync(BatchCreateVm vm);
    Task CloseAsync(BatchCloseVm vm);
    Task RecalcFarmStockAsync(int farmId);
    Task<List<BreedStandard>> GetBreedStandardAsync(string breed, string batchType);
    Task<List<DailyFarmRecord>> GetDailyRecordsAsync(int batchId);
    Task<List<FarmVisit>> GetVisitsAsync(int batchId);
    Task<List<BatchOrderRow>> GetOrdersAsync(int batchId);
    Task<List<MortalityLog>> GetMortalityLogsAsync(int batchId);
}
