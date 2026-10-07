using PoultryPro.Models.DTO;
using PoultryPro.Models.Entities;

public interface IFinanceService
{
    Task<IEnumerable<Transaction>> GetAllAsync();
    Task<int> CreateAsync(Transaction txn);
    Task<decimal> GetMonthlyRevenueAsync();
    Task<decimal> GetMonthlyExpensesAsync();
    Task<IEnumerable<MonthlyFinancial>> GetMonthlyTrendAsync(int months);

    // ── extended ──
    Task<IEnumerable<Transaction>> GetFilteredAsync(string? type, string? category, int? farmId, DateTime? from, DateTime? to);
    Task<Transaction?> GetByIdAsync(int id);
    Task UpdateAsync(Transaction txn);
    Task DeleteAsync(int id);
    Task<IEnumerable<string>> GetCategoriesAsync();
}
