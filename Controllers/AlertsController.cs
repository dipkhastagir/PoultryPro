using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;

namespace PoultryPro.Controllers;

/// <summary>Early-warning alert centre (rule engine in Services/Platform/AlertService.cs).</summary>
public class AlertsController(IAlertService alerts) : Controller
{
    /// <summary>Everyone except read-only viewers may act on alerts.</summary>
    private const string Actors = Roles.Admin + "," + Roles.Manager + "," + Roles.Veterinarian + "," + Roles.Accountant + "," + Roles.FieldOfficer;

    public async Task<IActionResult> Index(string? severity, string? rule, bool includeResolved = false)
    {
        await alerts.RefreshAsync();
        var list = (await alerts.GetAsync(includeResolved, severity, rule, 500)).ToList();
        var open = (await alerts.GetAsync(false, null, null, 1000)).ToList();
        ViewBag.Severity = severity;
        ViewBag.Rule = rule;
        ViewBag.IncludeResolved = includeResolved;
        ViewBag.Open = open;
        ViewData["Title"] = "Early-Warning Alerts";
        ViewData["Subtitle"] = "Rule-based alerts raised automatically from flock, sensor, health and stock data";
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Actors)]
    public async Task<IActionResult> Resolve(int id, string? returnUrl)
    {
        await alerts.ResolveAsync(id, User.UserName());
        TempData["Success"] = "Alert resolved.";
        return Back(returnUrl);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Actors)]
    public async Task<IActionResult> MarkRead(int id, string? returnUrl)
    {
        await alerts.MarkReadAsync(id);
        return Back(returnUrl);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Actors)]
    public async Task<IActionResult> MarkAllRead(string? returnUrl)
    {
        await alerts.MarkAllReadAsync();
        TempData["Success"] = "All open alerts marked as read.";
        return Back(returnUrl);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Actors)]
    public async Task<IActionResult> RunNow(string? returnUrl)
    {
        var n = await alerts.RefreshAsync(force: true);
        TempData["Success"] = n > 0 ? $"Rules evaluated — {n} new alert(s) raised." : "Rules evaluated — no new alerts.";
        return Back(returnUrl);
    }

    private IActionResult Back(string? returnUrl)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
}
