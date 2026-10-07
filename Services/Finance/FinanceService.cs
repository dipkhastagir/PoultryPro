using Dapper;
using PoultryPro.Models.DTO;
using PoultryPro.Models.Entities;


public class FinanceService(IDbConnectionFactory db) : IFinanceService
{
    private const string SelectSql = @"SELECT t.*, f.Name AS FarmName, b.BatchCode FROM Transactions t
              LEFT JOIN Farms f ON t.FarmId=f.Id LEFT JOIN Batches b ON b.Id=t.BatchId";

    public async Task<IEnumerable<Transaction>> GetAllAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Transaction>(SelectSql + " ORDER BY t.TxnDate DESC, t.Id DESC");
    }
    public async Task<IEnumerable<Transaction>> GetFilteredAsync(string? type, string? category, int? farmId, DateTime? from, DateTime? to)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Transaction>(SelectSql + @"
            WHERE (@type IS NULL OR t.TxnType=@type) AND (@category IS NULL OR t.Category=@category)
              AND (@farmId IS NULL OR t.FarmId=@farmId) AND (@from IS NULL OR t.TxnDate>=@from) AND (@to IS NULL OR t.TxnDate<=@to)
            ORDER BY t.TxnDate DESC, t.Id DESC",
            new { type = string.IsNullOrEmpty(type) ? null : type, category = string.IsNullOrEmpty(category) ? null : category, farmId, from, to });
    }
    public async Task<Transaction?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<Transaction>(SelectSql + " WHERE t.Id=@id", new { id });
    }
    public async Task<int> CreateAsync(Transaction txn)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Transactions(TxnDate,TxnType,Category,Description,Amount,PaymentStatus,FarmId,Reference,BatchId,PaymentMethod)
              VALUES(@TxnDate,@TxnType,@Category,@Description,@Amount,@PaymentStatus,@FarmId,@Reference,@BatchId,@PaymentMethod);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", txn);
    }
    public async Task UpdateAsync(Transaction txn)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE Transactions SET TxnDate=@TxnDate,TxnType=@TxnType,Category=@Category,Description=@Description,Amount=@Amount,
              PaymentStatus=@PaymentStatus,FarmId=@FarmId,Reference=@Reference,BatchId=@BatchId,PaymentMethod=@PaymentMethod WHERE Id=@Id", txn);
    }
    public async Task DeleteAsync(int id)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("DELETE FROM Transactions WHERE Id=@id", new { id });
    }
    public async Task<IEnumerable<string>> GetCategoriesAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<string>("SELECT DISTINCT Category FROM Transactions ORDER BY Category");
    }
    public async Task<decimal> GetMonthlyRevenueAsync()
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<decimal>(
            @"SELECT ISNULL(SUM(Amount),0) FROM Transactions
              WHERE TxnDate>=DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1) AND Amount>0");
    }
    public async Task<decimal> GetMonthlyExpensesAsync()
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<decimal>(
            @"SELECT ISNULL(SUM(ABS(Amount)),0) FROM Transactions
              WHERE TxnDate>=DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1) AND Amount<0");
    }
    public async Task<IEnumerable<MonthlyFinancial>> GetMonthlyTrendAsync(int months = 6)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<MonthlyFinancial>(
            "SELECT TOP(@months) * FROM vw_MonthlyFinancials ORDER BY [Year] DESC,[Month] DESC",
            new { months });
    }
}
