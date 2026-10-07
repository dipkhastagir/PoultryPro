using Dapper;
using PoultryPro.Models.Entities;

public interface IBudgetService
{
    Task<IEnumerable<Budget>> GetForMonthAsync(int year, int month);
    Task<Budget?> GetByIdAsync(int id);
    Task<int> CreateAsync(Budget b);
    Task UpdateAsync(Budget b);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int year, int month, string category, string budgetType, int? farmId, int excludeId = 0);
    Task<int> CopyFromPreviousMonthAsync(int year, int month);
    /// <summary>Actual revenue / expense per category for the month (or the whole year when month is null).</summary>
    Task<IEnumerable<CategoryActual>> GetActualsAsync(int year, int? month);
    Task<IEnumerable<int>> GetYearsWithDataAsync();
}

public class BudgetService(IDbConnectionFactory db) : IBudgetService
{
    public async Task<IEnumerable<Budget>> GetForMonthAsync(int year, int month)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Budget>(
            @"SELECT b.*, f.Name AS FarmName FROM Budgets b LEFT JOIN Farms f ON f.Id=b.FarmId
              WHERE b.[Year]=@year AND b.[Month]=@month ORDER BY b.BudgetType, b.PlannedAmount DESC", new { year, month });
    }

    public async Task<Budget?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<Budget>(
            "SELECT b.*, f.Name AS FarmName FROM Budgets b LEFT JOIN Farms f ON f.Id=b.FarmId WHERE b.Id=@id", new { id });
    }

    public async Task<int> CreateAsync(Budget b)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Budgets([Year],[Month],Category,BudgetType,PlannedAmount,FarmId,Notes)
              VALUES(@Year,@Month,@Category,@BudgetType,@PlannedAmount,@FarmId,@Notes);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", b);
    }

    public async Task UpdateAsync(Budget b)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE Budgets SET [Year]=@Year,[Month]=@Month,Category=@Category,BudgetType=@BudgetType,
              PlannedAmount=@PlannedAmount,FarmId=@FarmId,Notes=@Notes WHERE Id=@Id", b);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("DELETE FROM Budgets WHERE Id=@id", new { id });
    }

    public async Task<bool> ExistsAsync(int year, int month, string category, string budgetType, int? farmId, int excludeId = 0)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM Budgets WHERE [Year]=@year AND [Month]=@month AND Category=@category
              AND BudgetType=@budgetType AND ISNULL(FarmId,0)=ISNULL(@farmId,0) AND Id<>@excludeId",
            new { year, month, category, budgetType, farmId, excludeId }) > 0;
    }

    public async Task<int> CopyFromPreviousMonthAsync(int year, int month)
    {
        var prev = new DateTime(year, month, 1).AddMonths(-1);
        using var conn = db.Create();
        return await conn.ExecuteAsync(
            @"INSERT INTO Budgets([Year],[Month],Category,BudgetType,PlannedAmount,FarmId,Notes)
              SELECT @year,@month,p.Category,p.BudgetType,p.PlannedAmount,p.FarmId,'Copied from ' + @label
              FROM Budgets p
              WHERE p.[Year]=@py AND p.[Month]=@pm
                AND NOT EXISTS (SELECT 1 FROM Budgets c WHERE c.[Year]=@year AND c.[Month]=@month AND c.Category=p.Category
                                AND c.BudgetType=p.BudgetType AND ISNULL(c.FarmId,0)=ISNULL(p.FarmId,0))",
            new { year, month, py = prev.Year, pm = prev.Month, label = prev.ToString("MMM yyyy") });
    }

    public async Task<IEnumerable<CategoryActual>> GetActualsAsync(int year, int? month)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<CategoryActual>(
            @"SELECT MONTH(TxnDate) AS [Month], Category,
                     SUM(CASE WHEN Amount > 0 THEN Amount ELSE 0 END) AS Revenue,
                     SUM(CASE WHEN Amount < 0 THEN -Amount ELSE 0 END) AS Expense
              FROM Transactions
              WHERE YEAR(TxnDate)=@year AND (@month IS NULL OR MONTH(TxnDate)=@month)
              GROUP BY MONTH(TxnDate), Category", new { year, month });
    }

    public async Task<IEnumerable<int>> GetYearsWithDataAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<int>("SELECT DISTINCT YEAR(TxnDate) FROM Transactions ORDER BY 1 DESC");
    }
}
