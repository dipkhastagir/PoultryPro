using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

public class DashboardController(
    IDashboardService dashboardService,
    IDashboardInsightsService insights,
    IAlertService alerts,
    IFarmService farmService,
    ILogger<DashboardController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        // Re-evaluate alert rules on load (the service throttles itself); never let it break the dashboard
        try { await alerts.RefreshAsync(); }
        catch (Exception ex) { logger.LogWarning(ex, "Alert refresh failed on dashboard load"); }

        // Field officers and vets get an operations dashboard without money figures
        var showFinance = !User.IsInRole(Roles.FieldOfficer) && !User.IsInRole(Roles.Veterinarian);
        var vm = new DashboardVm
        {
            ShowFinance = showFinance,
            Summary = await dashboardService.GetSummaryAsync(),
            Farms = (await farmService.GetAllAsync()).ToList(),
            Alerts = (await alerts.GetOpenAsync(6)).ToList(),
            OpenAlertCount = await alerts.CountOpenAsync()
        };
        if (showFinance)
            vm.Monthly = (await dashboardService.GetMonthlyFinancialsAsync(6)).Reverse().ToList();
        await insights.FillAsync(vm);

        ViewData["Title"] = "Dashboard";
        ViewData["Subtitle"] = $"Operations overview · {DateTime.Today:dddd, dd MMM yyyy}";
        return View(vm);
    }
}
