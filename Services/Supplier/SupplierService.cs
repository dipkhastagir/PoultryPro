using Dapper;
using PoultryPro.Models.Entities;

public interface ISupplierService
{
    Task<IEnumerable<Supplier>> GetAllAsync(bool includeInactive = false);
    Task<Supplier?> GetByIdAsync(int id);
    Task<IEnumerable<SupplierPerformance>> GetPerformanceAsync();
    Task<SupplierPerformance?> GetPerformanceAsync(int supplierId);
    Task<IEnumerable<InventoryItem>> GetItemsAsync(int supplierId);
    Task<Dictionary<int, int>> GetItemCountsAsync();
    Task<IEnumerable<string>> GetCategoriesAsync();
    Task<int> CreateAsync(Supplier s);
    Task UpdateAsync(Supplier s);
    Task SetActiveAsync(int id, bool active);
}

public class SupplierService(IDbConnectionFactory db) : ISupplierService
{
    public async Task<IEnumerable<Supplier>> GetAllAsync(bool includeInactive = false)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Supplier>(
            "SELECT * FROM Suppliers WHERE (@includeInactive=1 OR IsActive=1) ORDER BY IsActive DESC, Name",
            new { includeInactive });
    }

    public async Task<Supplier?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<Supplier>("SELECT * FROM Suppliers WHERE Id=@id", new { id });
    }

    public async Task<IEnumerable<SupplierPerformance>> GetPerformanceAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<SupplierPerformance>("SELECT * FROM vw_SupplierPerformance");
    }

    public async Task<SupplierPerformance?> GetPerformanceAsync(int supplierId)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<SupplierPerformance>(
            "SELECT * FROM vw_SupplierPerformance WHERE Id=@supplierId", new { supplierId });
    }

    public async Task<IEnumerable<InventoryItem>> GetItemsAsync(int supplierId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<InventoryItem>(
            @"SELECT * FROM vw_InventoryStatus WHERE SupplierId=@supplierId
              UNION
              SELECT v.* FROM vw_InventoryStatus v
              WHERE v.Id IN (SELECT poi.InventoryItemId FROM PurchaseOrderItems poi
                             INNER JOIN PurchaseOrders po ON po.Id=poi.PurchaseOrderId
                             WHERE po.SupplierId=@supplierId)
              ORDER BY Category, Name", new { supplierId });
    }

    public async Task<Dictionary<int, int>> GetItemCountsAsync()
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<(int SupplierId, int Cnt)>(
            "SELECT SupplierId, COUNT(*) FROM InventoryItems WHERE SupplierId IS NOT NULL GROUP BY SupplierId");
        return rows.ToDictionary(r => r.SupplierId, r => r.Cnt);
    }

    public async Task<IEnumerable<string>> GetCategoriesAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<string>("SELECT DISTINCT Category FROM Suppliers ORDER BY Category");
    }

    public async Task<int> CreateAsync(Supplier s)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Suppliers(Name,ContactPerson,Phone,Email,Address,Category,Rating,PaymentTerms,IsActive)
              VALUES(@Name,@ContactPerson,@Phone,@Email,@Address,@Category,@Rating,@PaymentTerms,1);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", s);
    }

    public async Task UpdateAsync(Supplier s)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE Suppliers SET Name=@Name,ContactPerson=@ContactPerson,Phone=@Phone,Email=@Email,Address=@Address,
              Category=@Category,Rating=@Rating,PaymentTerms=@PaymentTerms,IsActive=@IsActive WHERE Id=@Id", s);
    }

    public async Task SetActiveAsync(int id, bool active)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("UPDATE Suppliers SET IsActive=@active WHERE Id=@id", new { id, active });
    }
}
