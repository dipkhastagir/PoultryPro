using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

/// <summary>Vaccination programme templates (age-day schedules) and applying them to flocks.</summary>
public class VaccineScheduleController(IVaccineScheduleService schedules, IBatchService batches) : Controller
{
    public static readonly string[] BatchTypes = { "Broiler", "Sonali", "Layer" };
    public static readonly string[] RoutesList = { "Eye drop", "Drinking water", "Spray", "Injection (SC)", "Injection (IM)", "Wing web", "Oral" };

    public async Task<IActionResult> Index(string? type = null)
    {
        var all = await schedules.GetTemplatesAsync();
        ViewBag.AllCount = all.Count;
        ViewBag.Type = string.IsNullOrWhiteSpace(type) ? null : type;
        var list = string.IsNullOrWhiteSpace(type) ? all : all.Where(t => t.BatchType == type).ToList();
        ViewBag.TypeCounts = BatchTypes.ToDictionary(t => t, t => all.Count(x => x.BatchType == t));
        ViewData["Title"] = "Vaccine Programmes";
        ViewData["Subtitle"] = "Age-based vaccination templates that auto-schedule doses for new flocks";
        return View(list);
    }

    public async Task<IActionResult> Detail(int id, int? batchId = null)
    {
        var t = await schedules.GetTemplateAsync(id);
        if (t == null) return NotFound();
        var vm = new VaccineTemplateDetailVm
        {
            Template = t,
            Items = (await schedules.GetItemsAsync(id)).OrderBy(i => i.AgeDay).ThenBy(i => i.Id).ToList(),
            ActiveBatches = (await batches.GetActiveAsync())
                .OrderBy(b => b.BatchType == t.BatchType ? 0 : 1).ThenBy(b => b.FarmName).ThenBy(b => b.BatchCode).ToList(),
            NewItem = new VaccineTemplateItem { TemplateId = id }
        };
        var last = vm.Items.LastOrDefault();
        if (last != null) vm.NewItem.AgeDay = last.AgeDay + 7;
        ViewBag.Routes = RoutesList;
        ViewBag.SelectedBatchId = batchId;
        ViewData["Title"] = t.Name;
        ViewData["Subtitle"] = $"{t.BatchType} vaccination programme · {vm.Items.Count} dose(s)";
        return View(vm);
    }

    [Authorize(Roles = Roles.Health)]
    public IActionResult Create(string? batchType = null)
    {
        SetFormMeta("New vaccine programme");
        return View("Form", new VaccineTemplate { BatchType = BatchTypes.Contains(batchType) ? batchType! : "Broiler", IsActive = true });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Create(VaccineTemplate model)
    {
        if (!BatchTypes.Contains(model.BatchType)) ModelState.AddModelError(nameof(model.BatchType), "Select a flock type.");
        if (!ModelState.IsValid) { SetFormMeta("New vaccine programme"); return View("Form", model); }
        var id = await schedules.CreateTemplateAsync(model);
        TempData["Success"] = $"Programme \"{model.Name}\" created — now add its vaccines.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Edit(int id)
    {
        var t = await schedules.GetTemplateAsync(id);
        if (t == null) return NotFound();
        SetFormMeta("Edit " + t.Name);
        return View("Form", t);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Edit(int id, VaccineTemplate model)
    {
        if (id != model.Id) return BadRequest();
        if (!BatchTypes.Contains(model.BatchType)) ModelState.AddModelError(nameof(model.BatchType), "Select a flock type.");
        if (!ModelState.IsValid) { SetFormMeta("Edit " + model.Name); return View("Form", model); }
        await schedules.UpdateTemplateAsync(model);
        TempData["Success"] = "Programme updated.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> AddItem(VaccineTemplateItem item)
    {
        var t = await schedules.GetTemplateAsync(item.TemplateId);
        if (t == null) return NotFound();
        item.VaccineName = item.VaccineName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(item.VaccineName) || item.AgeDay < 0 || item.AgeDay > 1000 || item.DoseMl is < 0)
        {
            TempData["Error"] = "Enter a vaccine name and an age (0–1000 days); dose must not be negative.";
            return RedirectToAction(nameof(Detail), new { id = item.TemplateId });
        }
        await schedules.AddItemAsync(item);
        TempData["Success"] = $"{item.VaccineName} added at day {item.AgeDay}.";
        return RedirectToAction(nameof(Detail), new { id = item.TemplateId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> RemoveItem(int id)
    {
        var templateId = await schedules.RemoveItemAsync(id);
        if (templateId == 0) { TempData["Error"] = "That vaccine was already removed."; return RedirectToAction(nameof(Index)); }
        TempData["Success"] = "Vaccine removed from the programme.";
        return RedirectToAction(nameof(Detail), new { id = templateId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Apply(int templateId, int batchId)
    {
        var t = await schedules.GetTemplateAsync(templateId);
        if (t == null) return NotFound();
        var b = await batches.GetByIdAsync(batchId);
        if (b == null || !b.IsActive)
        {
            TempData["Error"] = "Select an active flock to apply the programme to.";
            return RedirectToAction(nameof(Detail), new { id = templateId });
        }
        var inserted = await schedules.ApplyAsync(batchId, templateId);
        TempData[inserted > 0 ? "Success" : "Error"] = inserted > 0
            ? $"{inserted} vaccination(s) scheduled for {b.BatchCode} from \"{t.Name}\"."
            : $"No new vaccinations added — {b.BatchCode} already has every dose of \"{t.Name}\" scheduled.";
        return RedirectToAction(nameof(Detail), new { id = templateId, batchId });
    }

    private void SetFormMeta(string title)
    {
        ViewBag.BatchTypes = BatchTypes;
        ViewData["Title"] = title;
        ViewData["Subtitle"] = "Programme header — vaccines are added on the programme page";
    }
}
