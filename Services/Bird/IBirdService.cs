using PoultryPro.Models;
using PoultryPro.Models.Entities;


public interface IBirdService
{
    Task<IEnumerable<Batch>> GetAllAsync();
    Task<Batch?> GetByIdAsync(int id);
    Task<int> CreateAsync(Batch batch);
    Task UpdateAsync(Batch batch);
    Task<IEnumerable<MortalityLog>> GetMortalityLogsAsync();
    Task AddMortalityLogAsync(MortalityLog log);
    Task<IEnumerable<WeightRecord>> GetWeightRecordsAsync(int batchId);
    Task AddWeightRecordAsync(WeightRecord record);
}