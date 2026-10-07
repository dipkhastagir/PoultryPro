using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;

namespace PoultryPro.Controllers;

/// <summary>Shown when SQL Server cannot be reached; lets the user retry the automatic set-up.</summary>
[AllowAnonymous]
public class SetupController(DbStatus status, DatabaseInitializer initializer, IConfiguration config) : Controller
{
    public IActionResult Index()
    {
        if (status.IsReady) return RedirectToAction("Index", "Dashboard");
        ViewBag.Configured = config.GetConnectionString("Default");
        return View(status);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry()
    {
        var ok = await initializer.RunAsync();
        return ok ? RedirectToAction("Login", "Account") : RedirectToAction(nameof(Index));
    }
}
