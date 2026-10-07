using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;

namespace PoultryPro.Controllers;

public class HealthController(
    IHealthService healthService,
    IBirdService birdService,
    IDiseaseLibraryService diseaseLibrary) : Controller
{
    public static readonly string[] Routes = { "Eye drop", "Drinking water", "Spray", "Injection (SC)", "Injection (IM)", "Wing web", "Nasal drop", "Oral" };
    public static readonly string[] CaseStatuses = { "Active", "Under Treatment", "Monitoring", "Resolved" };

    public async Task<IActionResult> Index()
    {
        var vax = (await healthService.GetVaccinationsAsync()).ToList();
        var diseases = (await healthService.GetDiseaseCasesAsync()).ToList();
        var compliance = await healthService.GetComplianceByBatchAsync(true);
        ViewBag.Vaccinations = vax;
        ViewBag.DiseaseCases = diseases;
        ViewBag.Compliance = compliance;
        ViewBag.OverdueCount = vax.Count(v => v.IsOverdue);
        ViewBag.UpcomingCount = vax.Count(v => !v.IsOverdue && v.CompletedDate == null && v.DueDate <= DateTime.Today.AddDays(7));
        ViewBag.ActiveDiseases = diseases.Count(d => d.Status != "Resolved");
        var due = compliance.Sum(c => c.DueToDate);
        ViewBag.OverallCompliance = due > 0 ? Math.Round((decimal)compliance.Sum(c => c.Done) / due * 100, 1) : 100m;
        ViewData["Title"] = "Health Monitoring";
        ViewData["Subtitle"] = "Vaccination schedule & compliance, disease tracking and treatments";
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> MarkDone(int id, string? returnUrl)
    {
        await healthService.MarkVaccinationDoneAsync(id, User.FullName());
        TempData["Success"] = "Vaccination marked as done.";
        return BackTo(returnUrl);
    }

    [Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> AddVaccination(int? batchId)
    {
        ViewBag.Batches = await birdService.GetAllAsync();
        ViewData["Title"] = "Schedule Vaccination";
        ViewData["Subtitle"] = "Add a single vaccination to a flock's programme";
        return View(new VaccinationRecord { BatchId = batchId ?? 0, DueDate = DateTime.Today.AddDays(7), Status = "Planned" });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> AddVaccination(VaccinationRecord record)
    {
        if (record.BatchId <= 0 || string.IsNullOrWhiteSpace(record.VaccineName))
        {
            TempData["Error"] = "Select a flock and a vaccine.";
            ViewBag.Batches = await birdService.GetAllAsync();
            ViewData["Title"] = "Schedule Vaccination";
            ViewData["Subtitle"] = "Add a single vaccination to a flock's programme";
            return View(record);
        }
        record.VaccineName = record.VaccineName.Trim();
        if (record.Status == "Done")
            record.CompletedDate ??= DateTime.Today;
        else
            record.CompletedDate = null;
        await healthService.CreateVaccinationAsync(record);
        TempData["Success"] = "Vaccination scheduled.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> EditVaccination(int id, string? returnUrl)
    {
        var v = await healthService.GetVaccinationAsync(id);
        if (v == null) return NotFound();
        ViewBag.ReturnUrl = returnUrl;
        ViewData["Title"] = "Edit Vaccination";
        ViewData["Subtitle"] = $"{v.VaccineName} — flock {v.BatchCode}";
        return View(v);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> EditVaccination(int id, VaccinationRecord form, string? returnUrl)
    {
        var v = await healthService.GetVaccinationAsync(id);
        if (v == null) return NotFound();
        if (string.IsNullOrWhiteSpace(form.VaccineName))
        {
            TempData["Error"] = "Vaccine name is required.";
            ViewBag.ReturnUrl = returnUrl;
            ViewData["Title"] = "Edit Vaccination";
            ViewData["Subtitle"] = $"{v.VaccineName} — flock {v.BatchCode}";
            return View(v);
        }
        v.VaccineName = form.VaccineName.Trim();
        v.DueDate = form.DueDate;
        v.Route = form.Route;
        v.DoseMl = form.DoseMl;
        v.AdministeredBy = form.AdministeredBy;
        v.Notes = form.Notes;
        v.Status = string.IsNullOrWhiteSpace(form.Status) ? v.Status : form.Status;
        v.CompletedDate = form.CompletedDate;
        if (v.Status == "Done" && v.CompletedDate == null) v.CompletedDate = DateTime.Today;
        if (v.CompletedDate.HasValue && v.Status != "Done") v.Status = "Done";
        if (v.Status != "Done") v.CompletedDate = null;
        if (v.Status == "Planned" && v.DueDate < DateTime.Today) v.Status = "Overdue";
        if (v.Status == "Overdue" && v.DueDate >= DateTime.Today) v.Status = "Planned";
        await healthService.UpdateVaccinationAsync(v);
        TempData["Success"] = $"Vaccination {v.VaccineName} updated.";
        return BackTo(returnUrl);
    }

    [Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> AddDisease(int? batchId, string? disease)
    {
        ViewBag.Batches = await birdService.GetAllAsync();
        ViewBag.Library = (await diseaseLibrary.GetAllAsync()).Select(d => d.Name).ToList();
        ViewData["Title"] = "Log Disease Case";
        ViewData["Subtitle"] = "Record a clinical case, diagnosis and treatment";
        return View(new DiseaseCase { BatchId = batchId ?? 0, DiseaseName = disease ?? "", ReportedDate = DateTime.Today, Status = "Active", DiagnosedBy = User.FullName() });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> AddDisease(DiseaseCase dc)
    {
        if (dc.BatchId <= 0 || string.IsNullOrWhiteSpace(dc.DiseaseName))
        {
            TempData["Error"] = "Select a flock and enter the disease name.";
            ViewBag.Batches = await birdService.GetAllAsync();
            ViewBag.Library = (await diseaseLibrary.GetAllAsync()).Select(d => d.Name).ToList();
            ViewData["Title"] = "Log Disease Case";
            ViewData["Subtitle"] = "Record a clinical case, diagnosis and treatment";
            return View(dc);
        }
        dc.DiseaseName = dc.DiseaseName.Trim();
        if (string.IsNullOrWhiteSpace(dc.DiagnosedBy)) dc.DiagnosedBy = User.FullName(); // pre-filled on GET but not an input on the form
        dc.AffectedCount = Math.Max(0, dc.AffectedCount);
        dc.DeathCount = Math.Max(0, dc.DeathCount);
        await healthService.CreateDiseaseCaseAsync(dc);
        TempData["Success"] = "Disease case logged.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> EditDisease(int id, string? returnUrl)
    {
        var dc = await healthService.GetDiseaseCaseAsync(id);
        if (dc == null) return NotFound();
        ViewBag.ReturnUrl = returnUrl;
        ViewData["Title"] = "Update Disease Case";
        ViewData["Subtitle"] = $"{dc.DiseaseName} — flock {dc.BatchCode} ({dc.FarmName})";
        return View(dc);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> EditDisease(int id, DiseaseCase form, string? returnUrl)
    {
        var dc = await healthService.GetDiseaseCaseAsync(id);
        if (dc == null) return NotFound();
        dc.Treatment = form.Treatment ?? "";
        dc.Status = CaseStatuses.Contains(form.Status) ? form.Status : dc.Status;
        dc.AffectedCount = Math.Max(0, form.AffectedCount);
        dc.DeathCount = Math.Max(0, form.DeathCount);
        dc.DiagnosedBy = form.DiagnosedBy;
        dc.Notes = form.Notes;
        dc.ResolvedDate = dc.Status == "Resolved" ? (form.ResolvedDate ?? DateTime.Today) : null;
        if (dc.DeathCount > dc.AffectedCount && dc.AffectedCount > 0)
        {
            TempData["Error"] = "Deaths cannot exceed the number of affected birds.";
            ViewBag.ReturnUrl = returnUrl;
            ViewData["Title"] = "Update Disease Case";
            ViewData["Subtitle"] = $"{dc.DiseaseName} — flock {dc.BatchCode} ({dc.FarmName})";
            return View(dc);
        }
        await healthService.UpdateDiseaseCaseAsync(dc);
        TempData["Success"] = $"Case {dc.DiseaseName} on {dc.BatchCode} updated.";
        return BackTo(returnUrl);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> ResolveDisease(int id, string? returnUrl)
    {
        var dc = await healthService.GetDiseaseCaseAsync(id);
        if (dc != null)
        {
            dc.Status = "Resolved";
            dc.ResolvedDate = DateTime.Today;
            await healthService.UpdateDiseaseCaseAsync(dc);
            TempData["Success"] = "Disease case resolved.";
        }
        return BackTo(returnUrl);
    }

    private IActionResult BackTo(string? returnUrl)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
}
