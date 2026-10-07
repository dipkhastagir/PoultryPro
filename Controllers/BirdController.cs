using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;


namespace PoultryPro.Controllers;

public class BirdController(IBirdService birdService, IFarmService farmService, IBatchService batchService) : Controller
{
    private const string Writers = Roles.AdminOrManager + "," + Roles.Veterinarian;

    public async Task<IActionResult> Index()
    {
        var batches = await birdService.GetAllAsync();
        var logs = await birdService.GetMortalityLogsAsync();
        ViewBag.MortalityLogs = logs.Take(10).ToList();
        return View(batches.ToList());
    }

    public async Task<IActionResult> Detail(int id)
    {
        var batch = await birdService.GetByIdAsync(id);
        if (batch == null) return NotFound();
        var weights = await birdService.GetWeightRecordsAsync(id);
        ViewBag.WeightRecords = weights.ToList();
        return View(batch);
    }

    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Create()
    {
        ViewBag.Farms = await farmService.GetAllAsync();
        return View(new Batch { PlacementDate = DateTime.Today });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Writers)]
    public async Task<IActionResult> Create(Batch batch)
    {
        ModelState.KeepPostedOnly(Request.Form); // FarmName, BatchType etc. are display-only on Batch
        batch.BatchCode = (batch.BatchCode ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(batch.BatchCode)) ModelState.AddModelError(nameof(batch.BatchCode), "Batch code is required.");
        else if (await batchService.CodeExistsAsync(batch.BatchCode)) ModelState.AddModelError(nameof(batch.BatchCode), $"Batch code {batch.BatchCode} already exists.");
        if (batch.InitialCount <= 0) ModelState.AddModelError(nameof(batch.InitialCount), "Initial count must be at least 1.");
        if (await farmService.GetByIdAsync(batch.FarmId) == null) ModelState.AddModelError(nameof(batch.FarmId), "Select a farm.");
        if (!ModelState.IsValid)
        {
            ViewBag.Farms = await farmService.GetAllAsync();
            return View(batch);
        }
        batch.CurrentCount = batch.InitialCount;
        await birdService.CreateAsync(batch);
        TempData["Success"] = $"Batch {batch.BatchCode} created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Edit(int id)
    {
        var batch = await birdService.GetByIdAsync(id);
        if (batch == null) return NotFound();
        return View(batch);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Writers)]
    public async Task<IActionResult> Edit(Batch batch)
    {
        await birdService.UpdateAsync(batch);
        TempData["Success"] = "Batch updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Writers)]
    public async Task<IActionResult> AddMortality(MortalityLog log)
    {
        log.LogDate = DateTime.Today;
        await birdService.AddMortalityLogAsync(log);
        TempData["Success"] = "Mortality log added.";
        return RedirectToAction(nameof(Detail), new { id = log.BatchId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Writers)]
    public async Task<IActionResult> AddWeight(WeightRecord record)
    {
        record.RecordDate = DateTime.Today;
        if (record.SampleSize <= 0) record.SampleSize = 50; // not on the form; matches the column default
        await birdService.AddWeightRecordAsync(record);
        TempData["Success"] = "Weight record added.";
        return RedirectToAction(nameof(Detail), new { id = record.BatchId });
    }
}
