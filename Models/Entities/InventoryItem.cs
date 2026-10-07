namespace PoultryPro.Models.Entities
{
    public class InventoryItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? SKU { get; set; }
        public string Category { get; set; } = "";
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "";
        public decimal MinimumLevel { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? SupplierName { get; set; }
        public int? SupplierId { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalValue { get; set; }
        public string StockStatus { get; set; } = "In Stock";
        public bool IsExpiringSoon { get; set; }

        /// <summary>True when the expiry date has already passed.</summary>
        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.Today;
        /// <summary>Days until expiry (negative when expired), null when no expiry.</summary>
        public int? DaysToExpiry => ExpiryDate.HasValue ? (int)(ExpiryDate.Value.Date - DateTime.Today).TotalDays : null;
        /// <summary>Suggested re-order quantity: bring stock back to twice the minimum level.</summary>
        public decimal SuggestedReorderQty => Math.Max(0, MinimumLevel * 2 - Quantity);
    }
}
