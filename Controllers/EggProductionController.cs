using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

public class EggProductionController(IEggProductionService eggService) : Controller
{
    private const string WriteRoles = Roles.AdminOrManager + "," + Roles.Veterinarian;

    public async Task<IActionResult> Index(int? batchId, int days = 90)
    {
        days = Math.Clamp(days, 7, 365);
        var batches = await eggService.GetLayerBatchesAsync();
        var selected = batchId.HasValue ? batches.FirstOrDefault(b => b.Id == batchId.Value) : null;
        if (batchId.HasValue && selected == null) batchId = null;

        var records = await eggService.GetRecordsAsync(batchId, days);
        var placement = batches.ToDictionary(b => b.Id, b => b.PlacementDate);

        var daily = records.GroupBy(r => r.RecordDate.Date).OrderBy(g => g.Key).Select(g =>
        {
            var hens = g.Sum(r => r.HenCount);
            var weighted = g.Where(r => r.AvgEggWeightGm.HasValue && r.TotalEggs > 0).ToList();
            var wEggs = weighted.Sum(r => r.TotalEggs);
            return new EggDailyPoint
            {
                Date = g.Key,
                Hens = hens,
                Eggs = g.Sum(r => r.TotalEggs),
                GradeA = g.Sum(r => r.GradeA),
                GradeB = g.Sum(r => r.GradeB),
                Cracked = g.Sum(r => r.CrackedEggs),
                Dirty = g.Sum(r => r.DirtyEggs),
                FeedKg = g.Sum(r => r.FeedKg ?? 0),
                AvgEggWeightGm = wEggs > 0 ? Math.Round(weighted.Sum(r => r.AvgEggWeightGm!.Value * r.TotalEggs) / wEggs, 1) : null,
                AgeWeeks = hens > 0
                    ? Math.Round(g.Sum(r => (placement.TryGetValue(r.BatchId, out var p) ? (r.RecordDate - p).Days / 7m : r.AgeWeeks) * r.HenCount) / hens, 2)
                    : 0
            };
        }).ToList();

        var last7 = daily.Where(d => d.Date > DateTime.Today.AddDays(-7)).ToList();
        var last30 = daily.Where(d => d.Date > DateTime.Today.AddDays(-30)).ToList();
        var feed30 = last30.Sum(d => d.FeedKg);
        var eggs30Fed = last30.Where(d => d.FeedKg > 0).Sum(d => d.Eggs);

        var vm = new EggProductionIndexVm
        {
            LayerBatches = batches,
            BatchId = batchId,
            Batch = selected,
            Records = records,
            Latest = records.OrderByDescending(r => r.RecordDate).FirstOrDefault(),
            AvgHenDay7 = last7.Sum(d => d.Hens) > 0 ? Math.Round(last7.Sum(d => d.Eggs) * 100m / last7.Sum(d => d.Hens), 1) : 0,
            PeakHenDay = daily.Count > 0 ? daily.Max(d => d.HenDayPct) : 0,
            Eggs30 = last30.Sum(d => d.Eggs),
            FeedPerDozen30 = eggs30Fed > 0 ? Math.Round(feed30 * 1000m / (eggs30Fed / 12m), 0) : 0
        };
        ViewBag.Daily = daily;
        ViewBag.Days = days;

        ViewData["Title"] = "Egg Production";
        ViewData["Subtitle"] = selected != null
            ? $"{selected.BatchCode} · {selected.Breed} · {selected.FarmName} — hen-day %, grading and feed efficiency"
            : "Layer flocks — hen-day production, egg grading and feed per dozen";
        return View(vm);
    }

    [Authorize(Roles = WriteRoles)]
    public async Task<IActionResult> Create(int? batchId)
    {
        var batches = await eggService.GetLayerBatchesAsync(includeClosed: false);
        var b = batchId.HasValue ? batches.FirstOrDefault(x => x.Id == batchId) : batches.Count == 1 ? batches[0] : null;
        var vm = new EggProductionFormVm { BatchId = b?.Id ?? 0, HenCount = b?.CurrentCount ?? 0, RecordDate = DateTime.Today };
        if (b != null)
        {
            // pre-fill feed with the standard 112 g/hen/day as a starting value
            vm.FeedKg = Math.Round(b.CurrentCount * 0.112m, 1);
            if (await eggService.ExistsAsync(b.Id, DateTime.Today))
                ViewBag.Warning = $"Today's record for {b.BatchCode} already exists — choose another date or edit the existing record.";
        }
        LoadForm(batches, "Record egg production");
        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = WriteRoles)]
    public async Task<IActionResult> Create(EggProductionFormVm vm)
    {
        var batches = await eggService.GetLayerBatchesAsync();
        await ValidateAsync(vm, batches);
        if (!ModelState.IsValid)
        {
            LoadForm(batches.Where(b => b.IsActive || b.Id == vm.BatchId).ToList(), "Record egg production");
            return View("Form", vm);
        }
        vm.Id = await eggService.CreateAsync(vm);
        TempData["Success"] = $"Saved {vm.TotalEggs:N0} eggs for {vm.RecordDate:dd MMM yyyy} — hen-day {(vm.HenCount > 0 ? vm.TotalEggs * 100m / vm.HenCount : 0):0.0}%.";
        return RedirectToAction(nameof(Index), new { batchId = vm.BatchId });
    }

    [Authorize(Roles = WriteRoles)]
    public async Task<IActionResult> Edit(int id)
    {
        var r = await eggService.GetByIdAsync(id);
        if (r == null) return NotFound();
        var vm = new EggProductionFormVm
        {
            Id = r.Id, BatchId = r.BatchId, RecordDate = r.RecordDate, HenCount = r.HenCount, TotalEggs = r.TotalEggs,
            GradeA = r.GradeA, GradeB = r.GradeB, CrackedEggs = r.CrackedEggs, DirtyEggs = r.DirtyEggs,
            AvgEggWeightGm = r.AvgEggWeightGm, FeedKg = r.FeedKg, Notes = r.Notes
        };
        var batches = await eggService.GetLayerBatchesAsync();
        LoadForm(batches.Where(b => b.IsActive || b.Id == vm.BatchId).ToList(), $"Edit egg record — {r.BatchCode}, {r.RecordDate:dd MMM yyyy}");
        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = WriteRoles)]
    public async Task<IActionResult> Edit(EggProductionFormVm vm)
    {
        if (await eggService.GetByIdAsync(vm.Id) == null) return NotFound();
        var batches = await eggService.GetLayerBatchesAsync();
        await ValidateAsync(vm, batches);
        if (!ModelState.IsValid)
        {
            LoadForm(batches.Where(b => b.IsActive || b.Id == vm.BatchId).ToList(), "Edit egg record");
            return View("Form", vm);
        }
        await eggService.UpdateAsync(vm);
        TempData["Success"] = $"Egg record for {vm.RecordDate:dd MMM yyyy} updated.";
        return RedirectToAction(nameof(Index), new { batchId = vm.BatchId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = WriteRoles)]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await eggService.GetByIdAsync(id);
        if (r == null) return NotFound();
        var batchId = await eggService.DeleteAsync(id);
        TempData["Success"] = $"Deleted egg record {r.BatchCode} · {r.RecordDate:dd MMM yyyy}.";
        return RedirectToAction(nameof(Index), new { batchId = batchId > 0 ? batchId : (int?)null });
    }

    private async Task ValidateAsync(EggProductionFormVm vm, List<Batch> batches)
    {
        var b = batches.FirstOrDefault(x => x.Id == vm.BatchId);
        if (vm.BatchId > 0 && b == null)
            ModelState.AddModelError(nameof(vm.BatchId), "Select a layer flock.");
        if (vm.RecordDate.Date > DateTime.Today)
            ModelState.AddModelError(nameof(vm.RecordDate), "The record date cannot be in the future.");
        if (b != null && vm.RecordDate.Date < b.PlacementDate.Date)
            ModelState.AddModelError(nameof(vm.RecordDate), $"The flock was placed on {b.PlacementDate:dd MMM yyyy}.");
        var graded = vm.GradeA + vm.GradeB + vm.CrackedEggs + vm.DirtyEggs;
        if (graded > vm.TotalEggs)
            ModelState.AddModelError(nameof(vm.TotalEggs),
                $"Grade A + Grade B + cracked + dirty = {graded:N0}, which exceeds the total of {vm.TotalEggs:N0} eggs.");
        if (vm.HenCount > 0 && vm.TotalEggs > vm.HenCount)
            ModelState.AddModelError(nameof(vm.TotalEggs), "Total eggs cannot exceed the number of hens (a hen lays at most one egg a day).");
        if (vm.BatchId > 0 && await eggService.ExistsAsync(vm.BatchId, vm.RecordDate, vm.Id))
            ModelState.AddModelError(nameof(vm.RecordDate),
                $"A production record for this flock on {vm.RecordDate:dd MMM yyyy} already exists — edit that record instead.");
    }

    private void LoadForm(List<Batch> batches, string title)
    {
        ViewBag.Batches = batches;
        ViewData["Title"] = title;
        ViewData["Subtitle"] = "Daily lay count, grading and feed — hen-day % and feed per dozen are calculated automatically";
    }
}
