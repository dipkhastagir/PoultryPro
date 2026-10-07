using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;
namespace PoultryPro.Controllers;

public class DailyRecordController(
    IDailyRecordService dailyRecordService,
    IBatchService batchService,
    IFarmService farmService,
    IVisitService visitService,
    ISettingsService settings) : Controller
{
    // ── INDEX — all records for a farm / batch ────────────────────────────
    public async Task<IActionResult> Index(int? farmId, int? batchId,
                                           DateTime? from, DateTime? to)
    {
        var (records, rangeFrom, rangeTo) = await QueryAsync(farmId, batchId, from, to);

        var allBatches = (await batchService.GetAllAsync()).ToList();
        ViewBag.Farms         = await farmService.GetAllAsync();
        ViewBag.Batches       = allBatches
            .Where(b => !farmId.HasValue || b.FarmId == farmId)
            .OrderByDescending(b => b.IsActive).ThenByDescending(b => b.PlacementDate).ToList();
        ViewBag.SelectedFarm  = farmId;
        ViewBag.SelectedBatch = batchId;
        ViewBag.SelectedBatchCode = batchId.HasValue ? allBatches.FirstOrDefault(b => b.Id == batchId)?.BatchCode : null;
        ViewBag.From          = rangeFrom;
        ViewBag.To            = rangeTo;
        ViewBag.TotalMortality = records.Sum(r => r.MortalityCount);
        ViewBag.TotalFeed      = records.Sum(r => r.FeedConsumptionKg);
        ViewBag.ThiWarning     = await settings.GetDecimalAsync("Alert.THIWarning", 80m);
        ViewBag.ThiCritical    = await settings.GetDecimalAsync("Alert.THICritical", 85m);

        ViewData["Title"]    = "Daily Farm Records";
        ViewData["Subtitle"] = "Bird count, feed, environment and treatment logs";
        return View(records);
    }

    // ── EXPORT — same filters as Index, as CSV ────────────────────────────
    public async Task<IActionResult> Export(int? farmId, int? batchId, DateTime? from, DateTime? to)
    {
        var (records, rangeFrom, rangeTo) = await QueryAsync(farmId, batchId, from, to);
        var warn = await settings.GetDecimalAsync("Alert.THIWarning", 80m);
        var crit = await settings.GetDecimalAsync("Alert.THICritical", 85m);
        var rows = records.OrderBy(r => r.RecordDate).ThenBy(r => r.BatchCode).Select(r =>
        {
            decimal? thi = r.TemperatureC.HasValue && r.HumidityPercent.HasValue
                ? PoultryMath.THI(r.TemperatureC.Value, r.HumidityPercent.Value) : null;
            return new
            {
                RecordDate = r.RecordDate.ToString("yyyy-MM-dd"), Farm = r.FarmName, Batch = r.BatchCode, r.Breed,
                Officer = r.FieldOfficerName, r.BirdCount, r.MortalityCount, r.CullingCount,
                r.FeedConsumptionKg, r.WaterConsumptionLtr, r.FeedPerBird,
                r.TemperatureC, r.HumidityPercent, THI = thi,
                HeatBand = thi.HasValue ? PoultryMath.HeatBand(thi.Value, warn, crit) : "",
                r.LightHours, r.AverageWeightGm, r.MedicineUsed, r.MedicineDosage, r.VaccineUsed,
                r.Remarks, r.SyncStatus, r.VisitId
            };
        });
        var scope = batchId.HasValue ? $"batch{batchId}" : farmId.HasValue ? $"farm{farmId}" : "all";
        return Csv.File(rows, $"daily-records-{scope}-{rangeFrom:yyyyMMdd}-{rangeTo:yyyyMMdd}.csv");
    }

    /// <summary>
    /// Shared filter: a selected batch shows its whole history unless dates are given;
    /// otherwise the window defaults to the last 30 days.
    /// </summary>
    private async Task<(List<DailyFarmRecord> Records, DateTime From, DateTime To)> QueryAsync(
        int? farmId, int? batchId, DateTime? from, DateTime? to)
    {
        IEnumerable<DailyFarmRecord> records;
        if (batchId.HasValue)
            records = await dailyRecordService.GetByBatchAsync(batchId.Value);
        else if (farmId.HasValue)
            records = await dailyRecordService.GetByFarmAsync(farmId.Value, from, to);
        else
            records = await dailyRecordService.GetAll();

        var list = records.ToList();
        DateTime rangeFrom, rangeTo;
        if (batchId.HasValue && !from.HasValue && !to.HasValue)
        {
            rangeFrom = list.Count > 0 ? list.Min(r => r.RecordDate).Date : DateTime.Today.AddDays(-30);
            rangeTo   = list.Count > 0 ? list.Max(r => r.RecordDate).Date : DateTime.Today;
        }
        else
        {
            rangeFrom = (from ?? DateTime.Today.AddDays(-30)).Date;
            rangeTo   = (to ?? DateTime.Today).Date;
            if (rangeTo < rangeFrom) (rangeFrom, rangeTo) = (rangeTo, rangeFrom);
            list = list.Where(r => r.RecordDate.Date >= rangeFrom && r.RecordDate.Date <= rangeTo).ToList();
        }
        return (list.OrderByDescending(r => r.RecordDate).ThenBy(r => r.FarmName).ToList(), rangeFrom, rangeTo);
    }

    // ── CREATE ────────────────────────────────────────────────────────────
    [Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> Create(int visitId)
    {
        var visit = await visitService.GetByIdAsync(visitId);
        if (visit == null) return NotFound();

        IEnumerable<Batch> batches = new List<Batch>();
        if (visit.FarmId > 0)
            batches = await batchService.GetAllByFarmAsync(visit.FarmId);

        ViewBag.Visit   = visit;
        ViewBag.Batches = batches.Where(b => b.IsActive).ToList();

        var vm = new DailyRecordCreateVm
        {
            VisitId    = visitId,
            FarmId     = visit.FarmId,
            BatchId    = visit.BatchId ?? 0,
            RecordDate = visit.VisitDate
        };

        // pre-fill bird count from batch
        if (vm.BatchId > 0)
        {
            var batch = await batchService.GetByIdAsync(vm.BatchId);
            if (batch != null) vm.BirdCount = batch.CurrentCount;
        }

        ViewData["Title"] = "Add Daily Record";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> Create(DailyRecordCreateVm vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Visit   = await visitService.GetByIdAsync(vm.VisitId);
            ViewBag.Batches = (await batchService.GetAllByFarmAsync(vm.FarmId))
                              .Where(b => b.IsActive).ToList();
            return View(vm);
        }
        await dailyRecordService.CreateAsync(vm);
        TempData["Success"] = "Daily record saved.";
        return RedirectToAction("Detail", "Visit", new { id = vm.VisitId });
    }

    // ── EDIT ──────────────────────────────────────────────────────────────
    [Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> Edit(int id)
    {
        var record = await dailyRecordService.GetByIdAsync(id);
        if (record == null) return NotFound();
        ViewData["Title"] = $"Edit Record — {record.BatchCode} ({record.RecordDate:dd MMM yyyy})";
        return View(record);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> Edit(DailyFarmRecord record, string? returnUrl)
    {
        await dailyRecordService.UpdateAsync(record);
        TempData["Success"] = "Record updated.";
        return Back(record.VisitId, returnUrl);
    }

    // ── DELETE ────────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> Delete(int id, int visitId, string? returnUrl)
    {
        await dailyRecordService.DeleteAsync(id);
        TempData["Success"] = "Record deleted.";
        return Back(visitId, returnUrl);
    }

    /// <summary>Records entered outside a field visit (VisitId = 0) return to the register, not to Visit/Detail/0.</summary>
    private IActionResult Back(int visitId, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return visitId > 0 ? RedirectToAction("Detail", "Visit", new { id = visitId }) : RedirectToAction(nameof(Index));
    }
}
