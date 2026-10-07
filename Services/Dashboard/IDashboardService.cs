

using PoultryPro.Models.DTO;

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync();
    Task<IEnumerable<MonthlyFinancial>> GetMonthlyFinancialsAsync(int months);
}