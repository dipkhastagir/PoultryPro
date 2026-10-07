using System.ComponentModel.DataAnnotations;
using PoultryPro.Models.DTO;
using PoultryPro.Models.Entities;

namespace PoultryPro.Models.ViewModels
{
    // ───────────── Suppliers ─────────────
    public class SupplierCardVm
    {
        public Supplier Supplier { get; set; } = new();
        public SupplierPerformance? Perf { get; set; }
        public int ItemCount { get; set; }
    }

    public class SupplierDetailVm
    {
        public Supplier Supplier { get; set; } = new();
        public SupplierPerformance Perf { get; set; } = new();
        public List<PurchaseOrder> Orders { get; set; } = new();
        public List<InventoryItem> Items { get; set; } = new();
        public decimal OpenOrderValue { get; set; }
        public decimal UnpaidValue { get; set; }
        public decimal? AvgLeadTimeDays { get; set; }
        public List<(string Label, decimal Amount)> MonthlySpend { get; set; } = new();
    }

    // ───────────── Purchase orders ─────────────
    public class PurchaseLineInput
    {
        public int InventoryItemId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }

    public class PurchaseOrderCreateVm
    {
        [Required(ErrorMessage = "Choose a supplier.")]
        [Range(1, int.MaxValue, ErrorMessage = "Choose a supplier.")]
        public int SupplierId { get; set; }
        public int? FarmId { get; set; }
        [Required] public DateTime OrderDate { get; set; } = DateTime.Today;
        public DateTime? ExpectedDate { get; set; } = DateTime.Today.AddDays(5);
        [StringLength(500)] public string? Notes { get; set; }
        /// <summary>"Draft" or "Ordered".</summary>
        public string SaveAs { get; set; } = "Draft";
        public List<PurchaseLineInput> Lines { get; set; } = new();
    }

    public class PurchaseIndexVm
    {
        public List<PurchaseOrder> Orders { get; set; } = new();
        public string? Status { get; set; }
        public string? Q { get; set; }
        public Dictionary<string, int> CountByStatus { get; set; } = new();
        public Dictionary<string, decimal> ValueByStatus { get; set; } = new();
        public decimal UnpaidReceived { get; set; }
        public int OverdueCount { get; set; }
    }

    public class PurchaseDetailVm
    {
        public PurchaseOrder Order { get; set; } = new();
        public List<PurchaseOrderLine> Lines { get; set; } = new();
        public string CompanyName { get; set; } = "";
        public string CompanyAddress { get; set; } = "";
        public string CompanyPhone { get; set; } = "";
    }

    // ───────────── Inventory ledger ─────────────
    public class StockLedgerVm
    {
        public int? ItemId { get; set; }
        public string? Type { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public InventoryItem? Item { get; set; }
        public List<InventoryItem> Items { get; set; } = new();
        public List<StockLedgerEntry> Entries { get; set; } = new();
        public decimal TotalIn { get; set; }
        public decimal TotalOut { get; set; }
        public decimal ValueIn { get; set; }
        public decimal ValueOut { get; set; }
        public decimal? OpeningBalance { get; set; }
        public decimal? ClosingBalance { get; set; }
    }

    // ───────────── Sales ─────────────
    public class OrderDetailVm
    {
        public Order Order { get; set; } = new();
        public CustomerAccount? Customer { get; set; }
        public SalesBatchOption? Batch { get; set; }
        public List<Transaction> Payments { get; set; } = new();
        public string CompanyName { get; set; } = "";
        public string CompanyAddress { get; set; } = "";
        public string CompanyPhone { get; set; } = "";
    }

    public class CustomerDetailVm
    {
        public CustomerAccount Customer { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
        public List<Transaction> Payments { get; set; } = new();
        public List<(string Label, decimal Amount)> MonthlySales { get; set; } = new();
    }

    // ───────────── Finance ─────────────
    public class FinanceIndexVm
    {
        public List<Transaction> Transactions { get; set; } = new();
        public string? Type { get; set; }
        public string? Category { get; set; }
        public int? FarmId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public List<string> Categories { get; set; } = new();
        public List<Farm> Farms { get; set; } = new();
        public decimal MonthlyRevenue { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public List<MonthlyFinancial> Trend { get; set; } = new();
        public decimal FilteredIncome => Transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
        public decimal FilteredExpense => Transactions.Where(t => t.Amount < 0).Sum(t => -t.Amount);
        public bool IsFiltered => !string.IsNullOrEmpty(Type) || !string.IsNullOrEmpty(Category) || FarmId.HasValue || From.HasValue || To.HasValue;
    }

    public class BudgetLineVm
    {
        public Budget? Budget { get; set; }
        public string Category { get; set; } = "";
        public string BudgetType { get; set; } = "Expense";
        public decimal Planned { get; set; }
        public decimal Actual { get; set; }
        /// <summary>Positive = favourable (under-spent expense or over-achieved revenue).</summary>
        public decimal Variance => BudgetType == "Revenue" ? Actual - Planned : Planned - Actual;
        public decimal PctUsed => Planned > 0 ? Math.Round(Actual * 100m / Planned, 1) : 0;
        public bool Unbudgeted => Budget == null;
    }

    public class BudgetPageVm
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string PeriodLabel => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
        public List<BudgetLineVm> Expenses { get; set; } = new();
        public List<BudgetLineVm> Revenues { get; set; } = new();
        public decimal ExpensePlanned => Expenses.Where(e => !e.Unbudgeted).Sum(e => e.Planned);
        public decimal ExpenseActual => Expenses.Sum(e => e.Actual);
        public decimal RevenuePlanned => Revenues.Where(e => !e.Unbudgeted).Sum(e => e.Planned);
        public decimal RevenueActual => Revenues.Sum(e => e.Actual);
        public int DaysElapsedPct { get; set; }
    }

    public class PnlLineVm
    {
        public string Category { get; set; } = "";
        public decimal[] Months { get; set; } = new decimal[12];
        public decimal Total => Months.Sum();
    }

    public class ProfitLossVm
    {
        public int Year { get; set; }
        public List<int> AvailableYears { get; set; } = new();
        public List<PnlLineVm> Revenue { get; set; } = new();
        public List<PnlLineVm> Expenses { get; set; } = new();
        public decimal[] RevenueTotals => Enumerable.Range(0, 12).Select(i => Revenue.Sum(r => r.Months[i])).ToArray();
        public decimal[] ExpenseTotals => Enumerable.Range(0, 12).Select(i => Expenses.Sum(r => r.Months[i])).ToArray();
        public decimal[] Net => Enumerable.Range(0, 12).Select(i => RevenueTotals[i] - ExpenseTotals[i]).ToArray();
    }

    // ───────────── Sales report ─────────────
    public class SalesBreakdownRow
    {
        public string Label { get; set; } = "";
        public int Orders { get; set; }
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        public decimal Revenue { get; set; }
        public decimal Paid { get; set; }
        public decimal Outstanding => Math.Max(0, Revenue - Paid);
        public decimal AvgPrice { get; set; }
    }

    public class MarketReportVm
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<SalesBreakdownRow> ByProduct { get; set; } = new();
        public List<SalesBreakdownRow> ByCustomer { get; set; } = new();
        public List<(string Label, decimal Revenue, int Orders)> Monthly { get; set; } = new();
        public List<Order> Receivables { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public decimal TotalCollected { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal AvgOrderValue => OrderCount > 0 ? TotalRevenue / OrderCount : 0;
        public decimal CollectionRate => TotalRevenue > 0 ? TotalCollected * 100m / TotalRevenue : 0;
    }
}
