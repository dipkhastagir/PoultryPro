using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

/// <summary>Poultry disease knowledge base linked to farm disease cases.</summary>
public class DiseaseLibraryController(IDiseaseLibraryService library) : Controller
{
    public static readonly string[] Severities = { "Low", "Medium", "High", "Critical" };

    public async Task<IActionResult> Index(string? category = null, string? q = null)
    {
        var vm = new DiseaseLibraryIndexVm
        {
            Entries = await library.GetAllAsync(category, q),
            Categories = await library.GetCategoriesAsync(),
            Category = string.IsNullOrWhiteSpace(category) ? null : category,
            Q = q,
            TotalEntries = await library.CountAsync()
        };
        ViewData["Title"] = "Disease Library";
        ViewData["Subtitle"] = "Field reference for poultry diseases common in Bangladesh, linked to recorded farm cases";
        return View(vm);
    }

    public async Task<IActionResult> Detail(int id)
    {
        var e = await library.GetByIdAsync(id);
        if (e == null) return NotFound();
        var vm = new DiseaseLibraryDetailVm { Entry = e, Cases = await library.GetCasesAsync(e.Name) };
        ViewData["Title"] = e.Name;
        ViewData["Subtitle"] = $"{e.Category} disease · {e.Severity} severity";
        return View(vm);
    }

    [Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Create()
    {
        await SetFormMeta("Add disease");
        return View("Form", new DiseaseLibraryEntry());
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Create(DiseaseLibraryEntry model)
    {
        Validate(model);
        if (!ModelState.IsValid) { await SetFormMeta("Add disease"); return View("Form", model); }
        var id = await library.CreateAsync(model);
        TempData["Success"] = $"{model.Name} added to the library.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await library.GetByIdAsync(id);
        if (e == null) return NotFound();
        await SetFormMeta("Edit " + e.Name);
        return View("Form", e);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Edit(int id, DiseaseLibraryEntry model)
    {
        if (id != model.Id) return BadRequest();
        Validate(model);
        if (!ModelState.IsValid) { await SetFormMeta("Edit " + model.Name); return View("Form", model); }
        await library.UpdateAsync(model);
        TempData["Success"] = "Library entry updated.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    private void Validate(DiseaseLibraryEntry m)
    {
        if (!Severities.Contains(m.Severity)) ModelState.AddModelError(nameof(m.Severity), "Select a severity.");
        if (string.IsNullOrWhiteSpace(m.Symptoms)) ModelState.AddModelError(nameof(m.Symptoms), "Describe the main clinical signs.");
    }

    private async Task SetFormMeta(string title)
    {
        ViewBag.Categories = await library.GetCategoriesAsync();
        ViewBag.Severities = Severities;
        ViewData["Title"] = title;
        ViewData["Subtitle"] = "Clinical signs, transmission, prevention and treatment guidance";
    }
}
