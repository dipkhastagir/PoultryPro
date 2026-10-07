namespace PoultryPro.Models.Entities
{
    /// <summary>Header row of dbo.PurchaseOrders (joined with supplier and farm names).</summary>
    public class PurchaseOrder
    {
        public int Id { get; set; }
        public string PoNumber { get; set; } = "";
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = "";
        public string? SupplierContact { get; set; }
        public string? SupplierPhone { get; set; }
        public string? SupplierEmail { get; set; }
        public string? SupplierAddress { get; set; }
        public string? SupplierPaymentTerms { get; set; }
        public int? FarmId { get; set; }
        public string? FarmName { get; set; }
        public string? FarmLocation { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public string Status { get; set; } = "Draft";
        public string PaymentStatus { get; set; } = "Unpaid";
        public decimal TotalAmount { get; set; }
        public string? Notes { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public int LineCount { get; set; }

        public bool IsOverdue => (Status == "Ordered" || Status == "Draft") && ExpectedDate.HasValue && ExpectedDate.Value.Date < DateTime.Today;
        public bool? OnTime => Status == "Received" && ExpectedDate.HasValue && ReceivedDate.HasValue
            ? ReceivedDate.Value.Date <= ExpectedDate.Value.Date : null;
        public int? LeadTimeDays => ReceivedDate.HasValue ? (int)(ReceivedDate.Value.Date - OrderDate.Date).TotalDays : null;
    }

    /// <summary>A line of dbo.PurchaseOrderItems with item details.</summary>
    public class PurchaseOrderLine
    {
        public int Id { get; set; }
        public int PurchaseOrderId { get; set; }
        public int InventoryItemId { get; set; }
        public string ItemName { get; set; } = "";
        public string? SKU { get; set; }
        public string Category { get; set; } = "";
        public string Unit { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal LineTotal { get; set; }
    }

    /// <summary>Row of dbo.vw_SupplierPerformance.</summary>
    public class SupplierPerformance
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public decimal Rating { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalSpend { get; set; }
        public int ReceivedOrders { get; set; }
        public int OnTimeOrders { get; set; }
        public decimal? OnTimePct => ReceivedOrders > 0 ? Math.Round(OnTimeOrders * 100m / ReceivedOrders, 1) : null;
    }

    /// <summary>dbo.Budgets row.</summary>
    public class Budget
    {
        public int Id { get; set; }
        public int Year { get; set; } = DateTime.Today.Year;
        public int Month { get; set; } = DateTime.Today.Month;
        public string Category { get; set; } = "";
        public string BudgetType { get; set; } = "Expense";
        public decimal PlannedAmount { get; set; }
        public int? FarmId { get; set; }
        public string? FarmName { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>One movement in the stock ledger (dbo.InventoryTransactions + item/farm/batch).</summary>
    public class StockLedgerEntry
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = "";
        public string? SKU { get; set; }
        public string Category { get; set; } = "";
        public string Unit { get; set; } = "";
        public string TxnType { get; set; } = "IN";
        public decimal Quantity { get; set; }
        public string? Reason { get; set; }
        public DateTime TxnDate { get; set; }
        public decimal? UnitCost { get; set; }
        public int? FarmId { get; set; }
        public string? FarmName { get; set; }
        public int? BatchId { get; set; }
        public string? BatchCode { get; set; }
        public string? Reference { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal CurrentQuantity { get; set; }

        /// <summary>Balance after this movement (computed in C#).</summary>
        public decimal RunningBalance { get; set; }
        public decimal SignedQuantity => TxnType == "IN" ? Quantity : -Quantity;
        public decimal Value => Quantity * (UnitCost ?? 0);
    }

    /// <summary>A customer with sales-ledger aggregates.</summary>
    public class CustomerAccount : Customer
    {
        public int OrderCount { get; set; }
        public int OpenOrders { get; set; }
        public decimal TotalBilled { get; set; }
        public decimal TotalPaid { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public decimal Outstanding => Math.Max(0, TotalBilled - TotalPaid);
        public decimal CreditUsedPct => CreditLimit > 0 ? Math.Round(Outstanding * 100m / CreditLimit, 1) : 0;
        public bool OverLimit => CreditLimit > 0 && Outstanding > CreditLimit;
    }

    /// <summary>Lightweight batch option for sales-order source selection.</summary>
    public class SalesBatchOption
    {
        public int Id { get; set; }
        public string BatchCode { get; set; } = "";
        public int FarmId { get; set; }
        public string Breed { get; set; } = "";
        public string BatchType { get; set; } = "";
        public int CurrentCount { get; set; }
        public int AgeInDays { get; set; }
        public decimal AverageWeightKg { get; set; }
    }

    /// <summary>Category actuals for one period: revenue (positive) and expense (absolute negative) totals.</summary>
    public class CategoryActual
    {
        public int Month { get; set; }
        public string Category { get; set; } = "";
        public decimal Revenue { get; set; }
        public decimal Expense { get; set; }
    }
}
