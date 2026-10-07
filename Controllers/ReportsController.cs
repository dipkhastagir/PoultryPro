using Microsoft.AspNetCore.Mvc;

namespace PoultryPro.Controllers;

public partial class ReportsController(
    IDashboardService dashboardService,
    IBirdService birdService,
    IFarmService farmService,
    IFinanceService financeService,
    IInventoryService inventoryService,
    IEmployeeService employeeService) : Controller
{
    public IActionResult Index() => View();

    public async Task<IActionResult> Production()
    {
        var batches = await birdService.GetAllAsync();
        var logs = await birdService.GetMortalityLogsAsync();
        ViewBag.Batches = batches.ToList();
        ViewBag.MortalityLogs = logs.ToList();
        ViewBag.TotalBirds = batches.Sum(b => b.CurrentCount);
        ViewBag.TotalMortality = batches.Sum(b => b.MortalityCount);
        ViewBag.AvgFCR = batches.Where(b => b.FCR > 0).DefaultIfEmpty()
            .Average(b => b?.FCR ?? 0);
        ViewBag.AvgWeight = batches.Any() ? batches.Average(b => b.AverageWeightKg) : 0;
        ViewData["Title"] = "Production Report";
        ViewData["Subtitle"] = "Batch-wise production, FCR and weight statistics";
        return View();
    }

    public async Task<IActionResult> Financial()
    {
        var trend = await financeService.GetMonthlyTrendAsync(12);
        var transactions = await financeService.GetAllAsync();
        var revenue = await financeService.GetMonthlyRevenueAsync();
        var expenses = await financeService.GetMonthlyExpensesAsync();
        ViewBag.Trend = trend.Reverse().ToList();
        ViewBag.Transactions = transactions.ToList();
        ViewBag.MonthlyRevenue = revenue;
        ViewBag.MonthlyExpenses = expenses;
        ViewBag.NetProfit = revenue - expenses;
        ViewBag.ProfitMargin = revenue > 0 ? (revenue - expenses) / revenue * 100 : 0;
        ViewData["Title"] = "Financial Report";
        ViewData["Subtitle"] = "Revenue, expenses and profit analysis";
        return View();
    }

    public async Task<IActionResult> Inventory()
    {
        var items = await inventoryService.GetAllAsync();
        var low = await inventoryService.GetLowStockAsync();
        ViewBag.Items = items.ToList();
        ViewBag.LowStock = low.ToList();
        ViewBag.TotalValue = items.Sum(i => i.TotalValue);
        ViewBag.CategoryGroups = items.GroupBy(i => i.Category)
            .Select(g => new { Category = g.Key, Count = g.Count(), Value = g.Sum(i => i.TotalValue) })
            .ToList();
        ViewData["Title"] = "Inventory Report";
        ViewData["Subtitle"] = "Stock levels, values and alerts";
        return View();
    }

    public async Task<IActionResult> Farm()
    {
        var farms = await farmService.GetAllAsync();
        var certs = await farmService.GetCertificationsAsync();
        ViewBag.Farms = farms.ToList();
        ViewBag.Certifications = certs.ToList();
        ViewBag.TotalCapacity = farms.Sum(f => f.Capacity);
        ViewBag.TotalStock = farms.Sum(f => f.CurrentStock);
        ViewData["Title"] = "Farm Report";
        ViewData["Subtitle"] = "Farm capacity, utilization and certifications";
        return View();
    }

    public async Task<IActionResult> HR()
    {
        var employees = await employeeService.GetAllAsync();
        ViewBag.Employees = employees.ToList();
        ViewBag.TotalPayroll = employees.Sum(e => e.MonthlySalary);
        ViewBag.OnDuty = employees.Count(e => e.Status == "On Duty");
        ViewBag.ByFarm = employees.GroupBy(e => e.FarmName ?? "All Farms")
            .Select(g => new { Farm = g.Key, Count = g.Count(), Payroll = g.Sum(e => e.MonthlySalary) })
            .ToList();
        ViewData["Title"] = "HR Report";
        ViewData["Subtitle"] = "Staff summary and payroll breakdown";
        return View();
    }
}
