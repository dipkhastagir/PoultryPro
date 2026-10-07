using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;


public interface IHealthService
{
    Task<IEnumerable<VaccinationRecord>> GetVaccinationsAsync();
    Task<int> CreateVaccinationAsync(VaccinationRecord record);
    Task MarkVaccinationDoneAsync(int id);
    Task<IEnumerable<DiseaseCase>> GetDiseaseCasesAsync();
    Task<int> CreateDiseaseCaseAsync(DiseaseCase dc);
    Task UpdateDiseaseCaseAsync(DiseaseCase dc);

    // ── v2 ──
    Task MarkVaccinationDoneAsync(int id, string? administeredBy);
    Task<VaccinationRecord?> GetVaccinationAsync(int id);
    Task UpdateVaccinationAsync(VaccinationRecord record);
    Task<DiseaseCase?> GetDiseaseCaseAsync(int id);
    Task<List<VaccinationRecord>> GetVaccinationsByBatchAsync(int batchId);
    Task<List<DiseaseCase>> GetDiseaseCasesByBatchAsync(int batchId);
    /// <summary>Vaccination compliance per active batch (done ÷ due-to-date).</summary>
    Task<List<VaccinationComplianceRow>> GetComplianceByBatchAsync(bool activeOnly = true);
    /// <summary>Data for Reports/Health.</summary>
    Task<HealthReportVm> GetHealthReportAsync(int months = 12);
}
