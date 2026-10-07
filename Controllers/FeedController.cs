using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;

namespace PoultryPro.Controllers;

public class FeedController(IFeedService feedService) : Controller
{
    private const string WriteRoles = Roles.AdminOrManager + "," + Roles.Veterinarian;
    public static readonly string[] Stages = { "Pre-Starter", "Starter", "Grower", "Finisher", "Developer", "Pre-Layer", "Layer", "Breeder" };
    public static readonly string[] BatchTypes = { "Broiler", "Sonali", "Layer" };

    public async Task<IActionResult> Index(string? type, bool inactive = false)
    {
        var all = await feedService.GetAllAsync();
        var list = all.Where(f => inactive || f.IsActive)
                      .Where(f => string.IsNullOrEmpty(type) || f.BatchType == type).ToList();
        ViewBag.Type = type;
        ViewBag.Inactive = inactive;
        ViewBag.AllCount = all.Count;
        ViewBag.ActiveCount = all.Count(f => f.IsActive);
        ViewBag.Types = BatchTypes.Union(all.Select(f => f.BatchType)).Distinct().ToList();
        ViewData["Title"] = "Feed Formulations";
        ViewData["Subtitle"] = "Stage rations by flock type — nutrient specification, cost and age range";
        return View(list);
    }

    [Authorize(Roles = WriteRoles)]
    public IActionResult Create(string? type)
    {
        LoadForm("New feed formulation");
        return View("Form", new FeedFormulation
        {
            BatchType = BatchTypes.Contains(type) ? type! : "Broiler",
            Stage = "Starter", IsActive = true
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = WriteRoles)]
    public async Task<IActionResult> Create(FeedFormulation f)
    {
        Validate(f);
        if (!ModelState.IsValid)
        {
            LoadForm("New feed formulation");
            return View("Form", f);
        }
        await feedService.CreateAsync(f);
        TempData["Success"] = $"Formulation \"{f.Name}\" added.";
        return RedirectToAction(nameof(Index), new { type = f.BatchType });
    }

    [Authorize(Roles = WriteRoles)]
    public async Task<IActionResult> Edit(int id)
    {
        var f = await feedService.GetByIdAsync(id);
        if (f == null) return NotFound();
        LoadForm($"Edit {f.Name}");
        return View("Form", f);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = WriteRoles)]
    public async Task<IActionResult> Edit(FeedFormulation f)
    {
        if (await feedService.GetByIdAsync(f.Id) == null) return NotFound();
        Validate(f);
        if (!ModelState.IsValid)
        {
            LoadForm($"Edit {f.Name}");
            return View("Form", f);
        }
        await feedService.UpdateAsync(f);
        TempData["Success"] = $"Formulation \"{f.Name}\" updated.";
        return RedirectToAction(nameof(Index), new { type = f.BatchType });
    }

    public async Task<IActionResult> Planner()
    {
        var vm = await feedService.GetPlannerAsync();
        ViewData["Title"] = "Feed Planner";
        ViewData["Subtitle"] = "Current stage ration and feed requirement for every active flock";
        return View(vm);
    }

    public async Task<IActionResult> PlannerCsv()
    {
        var vm = await feedService.GetPlannerAsync();
        var rows = vm.Rows.Select(r => new
        {
            r.Batch.BatchCode, r.Batch.FarmName, r.Batch.BatchType, r.Batch.Breed, r.AgeDays, r.Birds,
            Formulation = r.Formulation?.Name ?? "", Stage = r.Formulation?.Stage ?? "",
            r.FeedPerBirdGm, r.DailyKg, r.WeeklyKg, CostPerKg = r.Formulation?.CostPerKg ?? 0, r.CostPerDay,
            NextStage = r.NextFormulation?.Name ?? "", r.DaysToNextStage, r.FeedBasis
        }).ToList();
        return Csv.File(rows, $"feed-plan-{DateTime.Today:yyyyMMdd}.csv");
    }

    private void Validate(FeedFormulation f)
    {
        if (f.ToAgeDay < f.FromAgeDay)
            ModelState.AddModelError(nameof(f.ToAgeDay), "The end age must be on or after the start age.");
        if (f.CalciumPct is < 0 or > 10)
            ModelState.AddModelError(nameof(f.CalciumPct), "Calcium should be between 0 and 10%.");
        if (f.PhosphorusPct is < 0 or > 5)
            ModelState.AddModelError(nameof(f.PhosphorusPct), "Phosphorus should be between 0 and 5%.");
        if (f.LysinePct is < 0 or > 5)
            ModelState.AddModelError(nameof(f.LysinePct), "Lysine should be between 0 and 5%.");
    }

    private void LoadForm(string title)
    {
        ViewBag.Stages = Stages;
        ViewBag.BatchTypes = BatchTypes;
        ViewData["Title"] = title;
        ViewData["Subtitle"] = "Nutrient specification on an as-fed basis — ME in kcal/kg, minerals and amino acids in %";
    }
}
