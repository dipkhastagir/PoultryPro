using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace PoultryPro.Controllers;

public partial class ReportsController
{
    public async Task<IActionResult> Health(int months = 12)
    {
        months = Math.Clamp(months, 3, 36);
        var health = HttpContext.RequestServices.GetRequiredService<IHealthService>();
        var vm = await health.GetHealthReportAsync(months);
        ViewBag.Months = months;
        ViewData["Title"] = "Health Report";
        ViewData["Subtitle"] = "Vaccination compliance, disease burden and mortality causes across all farms";
        return View(vm);
    }
}
