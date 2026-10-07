

using Dapper;
using PoultryPro.Models.DTO;

public class DashboardService(IDbConnectionFactory db) : IDashboardService
{
    public async Task<DashboardSummary> GetSummaryAsync()
    {
        using var conn = db.Create();
        return await conn.QuerySingleAsync<DashboardSummary>("SELECT * FROM vw_DashboardSummary");
    }
    public async Task<IEnumerable<MonthlyFinancial>> GetMonthlyFinancialsAsync(int months = 6)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<MonthlyFinancial>(
            "SELECT TOP (@m) * FROM vw_MonthlyFinancials ORDER BY [Year] DESC, [Month] DESC",
            new { m = months });
    }
}