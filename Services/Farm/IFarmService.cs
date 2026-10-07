using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;


public interface IFarmService
{
    Task<IEnumerable<Farm>> GetAllAsync();
    Task<Farm?> GetByIdAsync(int id);
    Task<int> CreateAsync(Farm farm);
    Task UpdateAsync(Farm farm);
    Task DeleteAsync(int id);
    Task<IEnumerable<Certification>> GetCertificationsAsync();

    // ── v2: farm profile, geo & certifications ──
    Task<IEnumerable<FarmBenchmarkRow>> GetBenchmarksAsync();
    Task<FarmDetailVm?> GetDetailAsync(int id);
    Task<FarmMapVm> GetMapAsync(int checkIns = 100);
    Task<IEnumerable<Certification>> GetCertificationsByFarmAsync(int farmId);
    Task<int> AddCertificationAsync(Certification cert);
    Task<int?> DeleteCertificationAsync(int id);
}
