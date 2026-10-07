using Dapper;
using PoultryPro.Models.Entities;

public class InventoryService(IDbConnectionFactory db) : IInventoryService
{
    public async Task<IEnumerable<InventoryItem>> GetAllAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<InventoryItem>(
            "SELECT * FROM vw_InventoryStatus ORDER BY Category,Name");
    }
    public async Task<InventoryItem?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<InventoryItem>(
            "SELECT * FROM vw_InventoryStatus WHERE Id=@id", new { id });
    }
    public async Task<int> CreateAsync(InventoryItem item)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO InventoryItems(Name,SKU,Category,Quantity,Unit,MinimumLevel,SupplierId,UnitCost,ExpiryDate)
              VALUES(@Name,@SKU,@Category,@Quantity,@Unit,@MinimumLevel,@SupplierId,@UnitCost,@ExpiryDate);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", item, tx);
        if (item.Quantity > 0)
            await conn.ExecuteAsync(
                @"INSERT INTO InventoryTransactions(ItemId,TxnType,Quantity,Reason,TxnDate,UnitCost,Reference)
                  VALUES(@id,'IN',@Quantity,'Opening balance',CAST(GETDATE() AS DATE),@UnitCost,'OPENING')",
                new { id, item.Quantity, item.UnitCost }, tx);
        tx.Commit();
        return id;
    }
    public async Task UpdateAsync(InventoryItem item)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE InventoryItems SET Name=@Name,SKU=@SKU,Category=@Category,Quantity=@Quantity,
              Unit=@Unit,MinimumLevel=@MinimumLevel,SupplierId=@SupplierId,
              UnitCost=@UnitCost,ExpiryDate=@ExpiryDate,UpdatedAt=GETDATE() WHERE Id=@Id", item);
    }
    public async Task DeleteAsync(int id)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"IF EXISTS (SELECT 1 FROM PurchaseOrderItems WHERE InventoryItemId=@id)
                  RAISERROR('This item is referenced by purchase orders and cannot be deleted.',16,1);
              ELSE
              BEGIN
                  DELETE FROM InventoryTransactions WHERE ItemId=@id;
                  DELETE FROM InventoryItems WHERE Id=@id;
              END", new { id });
    }
    public async Task<IEnumerable<InventoryItem>> GetLowStockAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<InventoryItem>(
            "SELECT * FROM vw_InventoryStatus WHERE StockStatus IN('Low Stock','Out of Stock') ORDER BY Category");
    }
    public async Task<IEnumerable<Supplier>> GetSuppliersAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Supplier>("SELECT * FROM Suppliers WHERE IsActive=1 ORDER BY Name");
    }
    public async Task StockAdjustAsync(int itemId, decimal qty, string type, string reason,
                                       int? farmId = null, string? reference = null, string? user = null, decimal? unitCost = null, DateTime? date = null)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var delta = type == "IN" ? qty : -qty;
        var txnDate = (date ?? DateTime.Today).Date;
        await conn.ExecuteAsync(
            @"UPDATE InventoryItems SET Quantity=Quantity+@delta,UpdatedAt=GETDATE() WHERE Id=@itemId;
              INSERT INTO InventoryTransactions(ItemId,TxnType,Quantity,Reason,TxnDate,UnitCost,FarmId,Reference,CreatedBy)
              VALUES(@itemId,@type,@qty,@reason,@txnDate,
                     ISNULL(@unitCost,(SELECT UnitCost FROM InventoryItems WHERE Id=@itemId)),@farmId,@reference,@user);",
            new { itemId, delta, type, qty, reason, txnDate, unitCost, farmId, reference, user }, tx);
        tx.Commit();
    }
    public async Task<IEnumerable<StockLedgerEntry>> GetLedgerAsync(int? itemId = null)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<StockLedgerEntry>(
            @"SELECT t.Id, t.ItemId, t.TxnType, t.Quantity, t.Reason, t.TxnDate, t.UnitCost, t.FarmId, t.BatchId,
                     t.Reference, t.CreatedBy, t.CreatedAt,
                     i.Name AS ItemName, i.SKU, i.Category, i.Unit, i.Quantity AS CurrentQuantity,
                     f.Name AS FarmName, b.BatchCode
              FROM InventoryTransactions t
              INNER JOIN InventoryItems i ON i.Id = t.ItemId
              LEFT JOIN Farms f ON f.Id = t.FarmId
              LEFT JOIN Batches b ON b.Id = t.BatchId
              WHERE (@itemId IS NULL OR t.ItemId=@itemId)
              ORDER BY t.TxnDate DESC, t.Id DESC", new { itemId });
    }
    public async Task<bool> SkuExistsAsync(string sku, int excludeId = 0)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM InventoryItems WHERE SKU=@sku AND Id<>@excludeId", new { sku, excludeId }) > 0;
    }
}
