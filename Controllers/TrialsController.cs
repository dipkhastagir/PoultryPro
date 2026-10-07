using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

/// <summary>Field trials: treatment vs control flock experiments with statistical comparison.</summary>
public class TrialsController(ITrialService trials) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Field Trials";
        ViewData["Subtitle"] = "Controlled on-farm experiments comparing a treatment flock with a control flock";
        return View((await trials.GetAllAsync()).ToList());
    }

    public async Task<IActionResult> Detail(int id)
    {
        var vm = await trials.GetDetailAsync(id);
        if (vm == null) return NotFound();
        ViewData["Title"] = $"Trial {vm.Trial.TrialCode}";
        ViewData["Subtitle"] = vm.Trial.Title;
        return View(vm);
    }

    [Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Create(int? treatmentId, int? controlId)
    {
        var t = new FieldTrial
        {
            TrialCode = await trials.NextCodeAsync(),
            PrincipalInvestigator = User.FullName(),
            TreatmentBatchId = treatmentId ?? 0,
            ControlBatchId = controlId ?? 0
        };
        return await Form(t, false);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Create([Bind(Prefix = "Trial")] FieldTrial t)
    {
        await Validate(t);
        if (!ModelState.IsValid) return await Form(t, false);
        var id = await trials.CreateAsync(t);
        TempData["Success"] = $"Trial {t.TrialCode} registered.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Edit(int id)
    {
        var t = await trials.GetByIdAsync(id);
        if (t == null) return NotFound();
        return await Form(t, true);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Trial")] FieldTrial t)
    {
        t.Id = id;
        await Validate(t);
        if (!ModelState.IsValid) return await Form(t, true);
        await trials.UpdateAsync(t);
        TempData["Success"] = $"Trial {t.TrialCode} updated.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Complete(int id, string status, string? conclusion)
    {
        var t = await trials.GetByIdAsync(id);
        if (t == null) return NotFound();
        status = status == "Abandoned" ? "Abandoned" : "Completed";
        if (string.IsNullOrWhiteSpace(conclusion))
        {
            TempData["Error"] = "Write a conclusion before closing the trial.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        await trials.CompleteAsync(id, status, conclusion.Trim());
        TempData["Success"] = $"Trial {t.TrialCode} marked as {status.ToLowerInvariant()}.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    private async Task Validate(FieldTrial t)
    {
        if (t.TreatmentBatchId > 0 && t.TreatmentBatchId == t.ControlBatchId)
            ModelState.AddModelError("Trial.ControlBatchId", "Treatment and control must be different flocks.");
        if (t.EndDate.HasValue && t.EndDate < t.StartDate)
            ModelState.AddModelError("Trial.EndDate", "End date cannot be before the start date.");
        if (!TrialFormVm.Metrics.Contains(t.PrimaryMetric))
            ModelState.AddModelError("Trial.PrimaryMetric", "Choose a primary metric from the list.");
        if (!TrialFormVm.Statuses.Contains(t.Status))
            ModelState.AddModelError("Trial.Status", "Invalid status.");
        if (!string.IsNullOrWhiteSpace(t.TrialCode) && await trials.CodeExistsAsync(t.TrialCode.Trim(), t.Id))
            ModelState.AddModelError("Trial.TrialCode", "This trial code is already used.");
        t.TrialCode = t.TrialCode?.Trim() ?? "";
    }

    private async Task<IActionResult> Form(FieldTrial t, bool edit)
    {
        ViewData["Title"] = edit ? $"Edit trial {t.TrialCode}" : "Register a field trial";
        ViewData["Subtitle"] = "Define the hypothesis, intervention and the treatment / control flocks";
        return View("Form", new TrialFormVm { Trial = t, IsEdit = edit, Batches = await trials.GetBatchOptionsAsync() });
    }
}
