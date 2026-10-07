using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.ViewModels;
using PoultryPro.Models.Entities;

namespace PoultryPro.Controllers;

public class FieldOfficersController(
    IFieldOfficerService fieldOfficerService,
    IFarmService farmService,
    IVisitService visitService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var officers  = await fieldOfficerService.GetAllAsync();
        var syncStats = await fieldOfficerService.GetPendingSyncSummaryAsync();
        var visits    = await fieldOfficerService.GetVisitsAsync();
        ViewBag.SyncStats      = syncStats.ToList();
        ViewBag.RecentVisits   = visits.Take(10).ToList();
        ViewBag.TotalOfficers  = officers.Count();
        ViewBag.ActiveOfficers = officers.Count(o => o.IsActive);
        ViewBag.PendingSyncs   = syncStats.Sum(s => s.TotalPending);
        ViewData["Title"]    = "Field Officers";
        ViewData["Subtitle"] = "Mobile app users, farm assignments and visit tracking";
        return View(officers.ToList());
    }

    public async Task<IActionResult> Detail(int id)
    {
        var officer = await fieldOfficerService.GetByIdAsync(id);
        if (officer == null) return NotFound();
        var assignments = await fieldOfficerService.GetAssignmentsAsync(id);
        var radius      = await visitService.GetGeofenceRadiusAsync();
        var visits      = VisitTrust.Apply(await fieldOfficerService.GetVisitsAsync(id), radius);
        var records     = await fieldOfficerService.GetDailyRecordsAsync(id);
        var (avgTrust, geoPct) = VisitTrust.Summarise(visits, radius);
        ViewBag.Assignments  = assignments.ToList();
        ViewBag.Visits       = visits;
        ViewBag.AvgTrust     = avgTrust;
        ViewBag.GeoVerifiedPct = geoPct;
        ViewBag.GeofenceRadius = radius;
        ViewBag.DailyRecords = records.Take(20).ToList();
        ViewData["Title"]    = officer.FullName;
        ViewData["Subtitle"] = $"{officer.Designation} — {officer.Zone ?? "All Zones"}";
        return View(officer);
    }

    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Create()
    {
        ViewBag.AllFarms = await farmService.GetAllAsync();
        ViewData["Title"] = "Add Field Officer";
        return View(new FieldOfficerCreateVm());
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Create(FieldOfficerCreateVm vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Password))
            ModelState.AddModelError("Password", "Password is required.");
        if (!ModelState.IsValid)
        {
            ViewBag.AllFarms = await farmService.GetAllAsync();
            return View(vm);
        }
        await fieldOfficerService.CreateAsync(vm);
        TempData["Success"] = $"Field Officer '{vm.FullName}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Edit(int id)
    {
        var officer = await fieldOfficerService.GetByIdAsync(id);
        if (officer == null) return NotFound();
        var assignments  = await fieldOfficerService.GetAssignmentsAsync(id);
        ViewBag.AllFarms = await farmService.GetAllAsync();
        var vm = new FieldOfficerEditVm
        {
            Id = officer.Id, FullName = officer.FullName, Username = officer.Username,
            Phone = officer.Phone, Email = officer.Email, Zone = officer.Zone,
            Designation = officer.Designation, IsActive = officer.IsActive,
            AssignedFarmIds = assignments.Select(a => a.FarmId).ToList()
        };
        ViewData["Title"] = $"Edit — {officer.FullName}";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Edit(FieldOfficerEditVm vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.AllFarms = await farmService.GetAllAsync();
            return View(vm);
        }
        await fieldOfficerService.UpdateAsync(vm);
        TempData["Success"] = $"'{vm.FullName}' updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        await fieldOfficerService.DeleteAsync(id);
        TempData["Success"] = "Field Officer removed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> ToggleActive(int id)
    {
        await fieldOfficerService.ToggleActiveAsync(id);
        TempData["Success"] = "Status updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> MarkSynced(int visitId, int officerId)
    {
        await fieldOfficerService.MarkVisitSyncedAsync(visitId);
        TempData["Success"] = "Visit marked as synced.";
        return RedirectToAction(nameof(Detail), new { id = officerId });
    }
}
