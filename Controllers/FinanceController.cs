using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

[Authorize(Roles = Roles.Finance + "," + Roles.Viewer)]
public class FinanceController(IFinanceService financeService, IFarmService farmService, IBudgetService budgetService) : Controller
{
    public static readonly string[] TxnTypes = { "Sale", "Purchase", "Expense", "Salary", "Investment" };
    public static readonly string[] DefaultCategories =
        { "Live Broiler", "Live Sonali", "Brown Eggs", "Spent Hen", "Customer Payment", "Feed", "Day-old Chicks", "Medicine & Vaccine",
          "Medicine", "Vaccine", "Supplement", "Biosecurity", "Equipment", "Salary", "Utilities", "Maintenance", "Transport", "Other" };
    public static readonly string[] PaymentMethods = { "Cash", "bKash", "Nagad", "Bank Transfer", "Cheque" };

    public async Task<IActionResult> Index(string? type, string? category, int? farmId, DateTime? from, DateTime? to)
    {
        var vm = new FinanceIndexVm
        {
            Type = type, Category = category, FarmId = farmId, From = from, To = to,
            Transactions = (await financeService.GetFilteredAsync(type, category, farmId, from, to)).ToList(),
            Categories = (await financeService.GetCategoriesAsync()).ToList(),
            Farms = (await farmService.GetAllAsync()).ToList(),
            MonthlyRevenue = await financeService.GetMonthlyRevenueAsync(),
            MonthlyExpenses = await financeService.GetMonthlyExpensesAsync(),
            Trend = (await financeService.GetMonthlyTrendAsync(12)).Reverse().ToList()
        };
        ViewData["Title"] = "Financial Management";
        ViewData["Subtitle"] = "General ledger — revenue, expenses and profitability";
        return View(vm);
    }

    public async Task<IActionResult> Export(string? type, string? category, int? farmId, DateTime? from, DateTime? to)
    {
        var rows = (await financeService.GetFilteredAsync(type, category, farmId, from, to)).Select(t => new
        {
            Date = t.TxnDate, Type = t.TxnType, t.Category, t.Description, t.Amount, t.PaymentStatus, t.PaymentMethod,
            Farm = t.FarmName, Batch = t.BatchCode, t.Reference
        });
        return Csv.File(rows, $"transactions-{DateTime.Today:yyyyMMdd}.csv");
    }

    [Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Create()
    {
        await FillTxnFormAsync("New Transaction");
        return View(new Transaction { TxnDate = DateTime.Today, PaymentStatus = "Paid", TxnType = "Expense" });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Create(Transaction txn, string amountSign)
    {
        ValidateTxn(txn);
        if (!ModelState.IsValid) { await FillTxnFormAsync("New Transaction"); return View(txn); }
        ApplySign(txn, amountSign);
        await financeService.CreateAsync(txn);
        TempData["Success"] = "Transaction saved.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Edit(int id)
    {
        var t = await financeService.GetByIdAsync(id);
        if (t == null) return NotFound();
        await FillTxnFormAsync("Edit Transaction");
        return View("Create", t);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Edit(Transaction txn, string amountSign)
    {
        if (await financeService.GetByIdAsync(txn.Id) == null) return NotFound();
        ValidateTxn(txn);
        if (!ModelState.IsValid) { await FillTxnFormAsync("Edit Transaction"); return View("Create", txn); }
        ApplySign(txn, amountSign);
        await financeService.UpdateAsync(txn);
        TempData["Success"] = "Transaction updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await financeService.GetByIdAsync(id);
        if (t == null) return NotFound();
        await financeService.DeleteAsync(id);
        TempData["Success"] = $"Transaction \"{t.Description}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    private static void ApplySign(Transaction txn, string? amountSign)
        => txn.Amount = amountSign == "negative" ? -Math.Abs(txn.Amount) : Math.Abs(txn.Amount);

    private void ValidateTxn(Transaction t)
    {
        ModelState.KeepPostedOnly(Request.Form);
        if (t.Amount == 0) ModelState.AddModelError(nameof(t.Amount), "Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(t.Description)) ModelState.AddModelError(nameof(t.Description), "Description is required.");
        if (string.IsNullOrWhiteSpace(t.Category)) ModelState.AddModelError(nameof(t.Category), "Category is required.");
        if (string.IsNullOrWhiteSpace(t.TxnType)) ModelState.AddModelError(nameof(t.TxnType), "Type is required.");
        if (t.TxnDate == default) ModelState.AddModelError(nameof(t.TxnDate), "Date is required.");
    }

    private async Task FillTxnFormAsync(string title)
    {
        ViewBag.Farms = (await farmService.GetAllAsync()).ToList();
        ViewBag.Categories = DefaultCategories.Union(await financeService.GetCategoriesAsync()).Distinct().OrderBy(c => c).ToList();
        ViewBag.Types = TxnTypes;
        ViewBag.PaymentMethods = PaymentMethods;
        ViewData["Title"] = title;
        ViewData["Subtitle"] = "Income is stored positive, costs negative";
    }

    // ───────────── Budgets ─────────────
    public async Task<IActionResult> Budget(int? year, int? month)
    {
        var y = year is >= 2000 and <= 2100 ? year.Value : DateTime.Today.Year;
        var m = Math.Clamp(month ?? DateTime.Today.Month, 1, 12);
        var budgets = (await budgetService.GetForMonthAsync(y, m)).ToList();
        var actuals = (await budgetService.GetActualsAsync(y, m)).ToList();

        var vm = new BudgetPageVm { Year = y, Month = m };
        // planned lines (aggregate farm-specific budgets under the same category for comparison)
        foreach (var b in budgets)
        {
            var act = actuals.Where(a => a.Category == b.Category).ToList();
            var line = new BudgetLineVm
            {
                Budget = b, Category = b.Category, BudgetType = b.BudgetType, Planned = b.PlannedAmount,
                Actual = b.BudgetType == "Revenue" ? act.Sum(a => a.Revenue) : act.Sum(a => a.Expense)
            };
            (b.BudgetType == "Revenue" ? vm.Revenues : vm.Expenses).Add(line);
        }
        // unbudgeted actuals
        foreach (var a in actuals.GroupBy(a => a.Category))
        {
            var exp = a.Sum(x => x.Expense); var rev = a.Sum(x => x.Revenue);
            if (exp > 0 && !budgets.Any(b => b.Category == a.Key && b.BudgetType == "Expense"))
                vm.Expenses.Add(new BudgetLineVm { Category = a.Key, BudgetType = "Expense", Actual = exp });
            if (rev > 0 && !budgets.Any(b => b.Category == a.Key && b.BudgetType == "Revenue"))
                vm.Revenues.Add(new BudgetLineVm { Category = a.Key, BudgetType = "Revenue", Actual = rev });
        }
        var first = new DateTime(y, m, 1);
        var days = DateTime.DaysInMonth(y, m);
        vm.DaysElapsedPct = DateTime.Today < first ? 0 : DateTime.Today >= first.AddMonths(1) ? 100 : (int)Math.Round(DateTime.Today.Day * 100.0 / days);
        ViewData["Title"] = "Budget vs Actual";
        ViewData["Subtitle"] = $"{vm.PeriodLabel} — planned vs posted transactions by category";
        return View(vm);
    }

    [Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> CreateBudget(int? year, int? month, string? category, string? type)
    {
        await FillBudgetFormAsync("New Budget Line");
        return View("BudgetForm", new Budget
        {
            Year = year ?? DateTime.Today.Year, Month = month ?? DateTime.Today.Month,
            Category = category ?? "", BudgetType = type == "Revenue" ? "Revenue" : "Expense"
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> CreateBudget(Budget b)
    {
        await ValidateBudgetAsync(b);
        if (!ModelState.IsValid) { await FillBudgetFormAsync("New Budget Line"); return View("BudgetForm", b); }
        await budgetService.CreateAsync(b);
        TempData["Success"] = $"Budget for {b.Category} ({new DateTime(b.Year, b.Month, 1):MMM yyyy}) saved.";
        return RedirectToAction(nameof(Budget), new { year = b.Year, month = b.Month });
    }

    [Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> EditBudget(int id)
    {
        var b = await budgetService.GetByIdAsync(id);
        if (b == null) return NotFound();
        await FillBudgetFormAsync("Edit Budget Line");
        return View("BudgetForm", b);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> EditBudget(Budget b)
    {
        if (await budgetService.GetByIdAsync(b.Id) == null) return NotFound();
        await ValidateBudgetAsync(b);
        if (!ModelState.IsValid) { await FillBudgetFormAsync("Edit Budget Line"); return View("BudgetForm", b); }
        await budgetService.UpdateAsync(b);
        TempData["Success"] = "Budget updated.";
        return RedirectToAction(nameof(Budget), new { year = b.Year, month = b.Month });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> DeleteBudget(int id)
    {
        var b = await budgetService.GetByIdAsync(id);
        if (b == null) return NotFound();
        await budgetService.DeleteAsync(id);
        TempData["Success"] = $"Budget line {b.Category} deleted.";
        return RedirectToAction(nameof(Budget), new { year = b.Year, month = b.Month });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> CopyBudget(int year, int month)
    {
        var n = await budgetService.CopyFromPreviousMonthAsync(year, month);
        TempData[n > 0 ? "Success" : "Error"] = n > 0 ? $"{n} budget line(s) copied from the previous month." : "Nothing to copy — previous month has no budget lines not already present.";
        return RedirectToAction(nameof(Budget), new { year, month });
    }

    private async Task ValidateBudgetAsync(Budget b)
    {
        ModelState.KeepPostedOnly(Request.Form);
        if (string.IsNullOrWhiteSpace(b.Category)) ModelState.AddModelError(nameof(b.Category), "Category is required.");
        if (b.PlannedAmount <= 0) ModelState.AddModelError(nameof(b.PlannedAmount), "Planned amount must be greater than zero.");
        if (b.Month is < 1 or > 12) ModelState.AddModelError(nameof(b.Month), "Choose a month.");
        if (b.Year is < 2000 or > 2100) ModelState.AddModelError(nameof(b.Year), "Enter a valid year.");
        if (b.BudgetType is not ("Expense" or "Revenue")) b.BudgetType = "Expense";
        if (ModelState.IsValid && await budgetService.ExistsAsync(b.Year, b.Month, b.Category.Trim(), b.BudgetType, b.FarmId, b.Id))
            ModelState.AddModelError(nameof(b.Category), "A budget line for this category, type, farm and month already exists — edit it instead.");
        b.Category = b.Category?.Trim() ?? "";
    }

    private async Task FillBudgetFormAsync(string title)
    {
        ViewBag.Farms = (await farmService.GetAllAsync()).ToList();
        ViewBag.Categories = DefaultCategories.Union(await financeService.GetCategoriesAsync()).Distinct().OrderBy(c => c).ToList();
        ViewData["Title"] = title;
        ViewData["Subtitle"] = "Monthly planned amount per category";
    }

    // ───────────── Profit & Loss ─────────────
    public async Task<IActionResult> ProfitLoss(int? year)
    {
        var y = year is >= 2000 and <= 2100 ? year.Value : DateTime.Today.Year;
        var actuals = (await budgetService.GetActualsAsync(y, null)).ToList();
        var vm = new ProfitLossVm { Year = y, AvailableYears = (await budgetService.GetYearsWithDataAsync()).ToList() };
        if (!vm.AvailableYears.Contains(y)) vm.AvailableYears.Add(y);
        vm.AvailableYears = vm.AvailableYears.Union(new[] { DateTime.Today.Year }).Distinct().OrderByDescending(v => v).ToList();
        foreach (var g in actuals.GroupBy(a => a.Category))
        {
            var rev = new PnlLineVm { Category = g.Key };
            var exp = new PnlLineVm { Category = g.Key };
            foreach (var a in g) { rev.Months[a.Month - 1] += a.Revenue; exp.Months[a.Month - 1] += a.Expense; }
            if (rev.Total != 0) vm.Revenue.Add(rev);
            if (exp.Total != 0) vm.Expenses.Add(exp);
        }
        vm.Revenue = vm.Revenue.OrderByDescending(r => r.Total).ToList();
        vm.Expenses = vm.Expenses.OrderByDescending(r => r.Total).ToList();
        ViewData["Title"] = "Profit & Loss Statement";
        ViewData["Subtitle"] = $"Fiscal year {y} — monthly revenue, expenses and net result by category";
        return View(vm);
    }
}
