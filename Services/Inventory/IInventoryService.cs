using PoultryPro.Models.Entities;


public interface IInventoryService
{
    Task<IEnumerable<InventoryItem>> GetAllAsync();
    Task<InventoryItem?> GetByIdAsync(int id);
    Task<int> CreateAsync(InventoryItem item);
    Task UpdateAsync(InventoryItem item);
    Task DeleteAsync(int id);
    Task<IEnumerable<InventoryItem>> GetLowStockAsync();
    Task<IEnumerable<Supplier>> GetSuppliersAsync();
    Task StockAdjustAsync(int itemId, decimal qty, string type, string reason,
                          int? farmId = null, string? reference = null, string? user = null, decimal? unitCost = null, DateTime? date = null);
    /// <summary>Stock movements (newest first), optionally for one item.</summary>
    Task<IEnumerable<StockLedgerEntry>> GetLedgerAsync(int? itemId = null);
    Task<bool> SkuExistsAsync(string sku, int excludeId = 0);
}
