using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

/// <summary>Farm-to-fork traceability: batch register with QR codes, internal lifecycle timeline and the public consumer page.</summary>
public class TraceController(ITraceService trace) : Controller
{
    public async Task<IActionResult> Index(string? q, string? status)
    {
        var rows = await trace.GetBatchesAsync(q, status);
        ViewBag.Q = q; ViewBag.Status = status;
        ViewBag.PublicBase = PublicUrl("");
        ViewData["Title"] = "Traceability";
        ViewData["Subtitle"] = "Farm-to-fork records and consumer QR codes for every flock";
        return View(rows);
    }

    public async Task<IActionResult> Timeline(int id)
    {
        var vm = await trace.GetTimelineAsync(id);
        if (vm == null) return NotFound();
        vm.PublicUrl = PublicUrl(vm.Batch.BatchCode);
        ViewData["Title"] = $"Trace — {vm.Batch.BatchCode}";
        ViewData["Subtitle"] = $"{vm.Batch.Breed} · {vm.Batch.FarmName} · placed {Fmt.Date(vm.Batch.PlacementDate)}";
        return View(vm);
    }

    /// <summary>Seals the batch record: stores the current hash-chain head, time and user so later edits become detectable.</summary>
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> Seal(int id, string? head)
    {
        var ok = await trace.SealAsync(id, User.FullName(), head);
        if (ok) TempData["Success"] = "Traceability record sealed. Any later change to this batch's history will be flagged.";
        else TempData["Error"] = "The record changed while you were reviewing it (or the batch no longer exists). Review the timeline and seal again.";
        return RedirectToAction(nameof(Timeline), new { id });
    }

    /// <summary>Public, anonymous consumer page reached by scanning the QR code on the product.</summary>
    [AllowAnonymous]
    [HttpGet("Trace/Public/{code?}")]
    public async Task<IActionResult> Public(string? code)
    {
        code ??= Request.Query["code"].ToString();
        var vm = await trace.GetPublicAsync(code ?? "");
        ViewData["Title"] = vm.Found ? $"Product origin — {vm.BatchCode}" : "Product origin";
        if (!vm.Found && !string.IsNullOrWhiteSpace(code)) Response.StatusCode = StatusCodes.Status404NotFound;
        return View(vm);
    }

    private string PublicUrl(string code)
        => $"{Request.Scheme}://{Request.Host}{Request.PathBase}/Trace/Public/{Uri.EscapeDataString(code)}";
}
