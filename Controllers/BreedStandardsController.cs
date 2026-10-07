using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

/// <summary>Breed performance standards (reference growth curves).</summary>
public class BreedStandardsController(IBreedStandardService standards) : Controller
{
    public async Task<IActionResult> Index(string? breed)
    {
        var breeds = await standards.GetBreedsAsync();
        var selected = breeds.FirstOrDefault(b => string.Equals(b.Breed, breed, StringComparison.OrdinalIgnoreCase)).Breed
                       ?? breeds.FirstOrDefault().Breed;
        ViewBag.Breeds = breeds;
        ViewBag.Selected = selected;
        ViewData["Title"] = "Breed Standards";
        ViewData["Subtitle"] = "Reference growth, feed, FCR and mortality curves used by the analytics engine";
        var points = selected != null ? await standards.GetByBreedAsync(selected) : new();
        return View(points);
    }

    [Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Create(string? breed)
    {
        var vm = new BreedStandardFormVm { Breed = breed ?? "" };
        if (!string.IsNullOrEmpty(breed))
        {
            var pts = await standards.GetByBreedAsync(breed);
            if (pts.Count > 0) vm.AgeDay = pts.Max(p => p.AgeDay) + 7;
        }
        return await Form(vm, false);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Create(BreedStandardFormVm vm)
    {
        if (ModelState.IsValid && await standards.ExistsAsync(vm.Breed.Trim(), vm.AgeDay))
            ModelState.AddModelError(nameof(vm.AgeDay), $"{vm.Breed} already has a point at day {vm.AgeDay}. Edit that row instead.");
        if (!ModelState.IsValid) return await Form(vm, false);
        await standards.CreateAsync(vm.ToEntity());
        TempData["Success"] = $"Standard point day {vm.AgeDay} added to {vm.Breed.Trim()}.";
        return RedirectToAction(nameof(Index), new { breed = vm.Breed.Trim() });
    }

    [Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Edit(int id)
    {
        var s = await standards.GetByIdAsync(id);
        if (s == null) return NotFound();
        return await Form(BreedStandardFormVm.From(s), true);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Edit(int id, BreedStandardFormVm vm)
    {
        vm.Id = id;
        if (ModelState.IsValid && await standards.ExistsAsync(vm.Breed.Trim(), vm.AgeDay, id))
            ModelState.AddModelError(nameof(vm.AgeDay), $"{vm.Breed} already has another point at day {vm.AgeDay}.");
        if (!ModelState.IsValid) return await Form(vm, true);
        await standards.UpdateAsync(vm.ToEntity());
        TempData["Success"] = $"{vm.Breed.Trim()} day {vm.AgeDay} updated.";
        return RedirectToAction(nameof(Index), new { breed = vm.Breed.Trim() });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Delete(int id)
    {
        var breed = await standards.DeleteAsync(id);
        TempData["Success"] = "Standard point deleted.";
        return RedirectToAction(nameof(Index), new { breed });
    }

    private async Task<IActionResult> Form(BreedStandardFormVm vm, bool edit)
    {
        vm.KnownBreeds = (await standards.GetBreedsAsync()).Select(b => b.Breed).ToList();
        ViewData["Title"] = edit ? "Edit breed standard point" : "Add breed standard point";
        ViewData["Subtitle"] = "Values should come from the official breed management guide";
        ViewBag.IsEdit = edit;
        return View("Form", vm);
    }
}
