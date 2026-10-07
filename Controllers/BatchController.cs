using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

/// <summary>Flock register and full flock lifecycle: placement → daily management → harvest / close.</summary>
public class BatchController(
    IBatchService batchService,
    IFarmService farmService,
    IHealthService healthService,
    IBirdService birdService,
    IVaccineScheduleService vaccineService,
    IEggProductionService eggService) : Controller
{
    private const string Writers = Roles.AdminOrManager + "," + Roles.Veterinarian;

    public static readonly string[] Breeds = { "Cobb 500", "Ross 308", "Sonali", "Hy-Line Brown", "Lohmann Brown", "Other" };
    public static readonly string[] BatchTypes = { "Broiler", "Sonali", "Layer" };
    public static readonly string[] HealthStatuses = { "Healthy", "Vaccination Due", "Under Observation", "Under Treatment", "Outbreak", "Ready for Sale" };

    // ───────────────────────────── REGISTER ─────────────────────────────
    public async Task<IActionResult> Index(int? farmId, string? status = "active")
    {
        status = (status ?? "active").ToLowerInvariant();
        if (status is not ("active" or "closed" or "all")) status = "active";

        var all = await batchService.GetRegisterAsync(farmId, "all");
        var vm = new FlockRegisterVm
        {
            FarmId = farmId,
            Status = status,
            Farms = (await farmService.GetAllAsync()).ToList(),
            ActiveCount = all.Count(b => b.IsActive),
            ClosedCount = all.Count(b => !b.IsActive),
            AllCount = all.Count,
            Batches = status switch
            {
                "closed" => all.Where(b => !b.IsActive).OrderByDescending(b => b.ClosedDate ?? b.PlacementDate).ToList(),
                "all"    => all,
                _        => all.Where(b => b.IsActive).ToList()
            },
            Health = (await batchService.GetHealthFlagsAsync()).ToDictionary(h => h.BatchId)
        };

        ViewData["Title"] = "Flocks / Batches";
        ViewData["Subtitle"] = "Flock register with livability, FCR and EPEF performance indicators";
        return View(vm);
    }

    public async Task<IActionResult> Export(int? farmId, string? status = "all")
    {
        var rows = await batchService.GetRegisterAsync(farmId, status ?? "all");
        return Csv.File(rows.Select(b => new
        {
            b.BatchCode, b.FarmName, b.Breed, b.BatchType, b.ShedNo, b.HatcheryName,
            PlacementDate = b.PlacementDate.ToString("yyyy-MM-dd"), ClosedDate = b.ClosedDate?.ToString("yyyy-MM-dd"),
            b.AgeInDays, b.InitialCount, b.CurrentCount, b.MortalityCount, b.CullingCount, b.LivabilityPct,
            b.AverageWeightKg, b.TotalFeedKg, b.FCR, b.EPEF, b.AvgDailyGainGm, b.HarvestedCount, b.HarvestWeightKg, b.HealthStatus
        }), $"flock-register-{DateTime.Today:yyyyMMdd}.csv");
    }

    // ───────────────────────────── DETAIL (lifecycle cockpit) ─────────────────────────────
    public async Task<IActionResult> Detail(int id)
    {
        var batch = await batchService.GetPerformanceAsync(id);
        if (batch == null) return NotFound();

        var vm = new BatchDetailVm
        {
            Batch = batch,
            DailyRecords = await batchService.GetDailyRecordsAsync(id),
            Standard = await batchService.GetBreedStandardAsync(batch.Breed, batch.BatchType),
            Vaccinations = await healthService.GetVaccinationsByBatchAsync(id),
            DiseaseCases = await healthService.GetDiseaseCasesByBatchAsync(id),
            Weights = (await birdService.GetWeightRecordsAsync(id)).ToList(),
            Visits = await batchService.GetVisitsAsync(id),
            Orders = await batchService.GetOrdersAsync(id),
            MortalityLogs = await batchService.GetMortalityLogsAsync(id),
            ChickCostTotal = batch.InitialCount * batch.ChickCostPerBird
        };
        if (batch.BatchType == "Layer")
            vm.EggRecords = await eggService.GetRecordsAsync(id, 3650);
        vm.SalesTotal = vm.Orders.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount);

        var std = PoultryMath.Standard(vm.Standard, Math.Max(batch.AgeInDays, 0));
        if (std.HasValue)
        {
            vm.StdWeightTodayGm = Math.Round(std.Value.WeightGm, 0);
            vm.StdFcrToday = Math.Round(std.Value.Fcr, 2);
            vm.StdCumMortToday = Math.Round(std.Value.CumMortPct, 2);
        }

        ViewData["Title"] = $"Flock {batch.BatchCode}";
        ViewData["Subtitle"] = $"{batch.Breed} · {batch.BatchType} · {batch.FarmName}{(string.IsNullOrEmpty(batch.ShedNo) ? "" : " / shed " + batch.ShedNo)}";
        return View(vm);
    }

    // ───────────────────────────── CREATE ─────────────────────────────
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Create(int? farmId)
    {
        var vm = new BatchCreateVm
        {
            FarmId = farmId ?? 0,
            BatchCode = await batchService.SuggestNextCodeAsync(),
            PlacementDate = DateTime.Today,
            ExpectedHarvestDate = DateTime.Today.AddDays(35),
            HatcheryName = "Kazi Hatchery"
        };
        await LoadFormListsAsync();
        var templates = (List<VaccineTemplate>)ViewBag.Templates;
        vm.VaccineTemplateId = templates.FirstOrDefault(t => t.BatchType == vm.BatchType)?.Id;
        ViewData["Title"] = "Place New Flock";
        ViewData["Subtitle"] = "Register a day-old chick placement, post the chick cost and schedule its vaccine programme";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Writers)]
    public async Task<IActionResult> Create(BatchCreateVm vm)
    {
        vm.BatchCode = (vm.BatchCode ?? "").Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(vm.BatchCode) && await batchService.CodeExistsAsync(vm.BatchCode))
            ModelState.AddModelError(nameof(vm.BatchCode), $"Batch code {vm.BatchCode} already exists.");
        if (!BatchTypes.Contains(vm.BatchType))
            ModelState.AddModelError(nameof(vm.BatchType), "Choose Broiler, Sonali or Layer.");
        if (vm.ExpectedHarvestDate.HasValue && vm.ExpectedHarvestDate.Value < vm.PlacementDate)
            ModelState.AddModelError(nameof(vm.ExpectedHarvestDate), "Expected harvest cannot be before placement.");
        if (vm.PlacementDate > DateTime.Today.AddDays(30))
            ModelState.AddModelError(nameof(vm.PlacementDate), "Placement date is too far in the future.");
        if (vm.ApplyVaccineProgramme && vm.VaccineTemplateId is null or 0)
            vm.ApplyVaccineProgramme = false;

        var farm = vm.FarmId > 0 ? await farmService.GetByIdAsync(vm.FarmId) : null;
        if (farm == null) ModelState.AddModelError(nameof(vm.FarmId), "Select a farm.");

        if (!ModelState.IsValid)
        {
            await LoadFormListsAsync();
            ViewData["Title"] = "Place New Flock";
            ViewData["Subtitle"] = "Register a day-old chick placement, post the chick cost and schedule its vaccine programme";
            return View(vm);
        }

        var (id, scheduled) = await batchService.CreateFlockAsync(vm);
        var msg = $"Flock {vm.BatchCode} placed with {vm.InitialCount:N0} birds.";
        if (scheduled > 0) msg += $" {scheduled} vaccinations scheduled.";
        if (vm.PostChickPurchase && vm.ChickCostPerBird > 0) msg += $" Chick purchase of {Fmt.Money(vm.InitialCount * vm.ChickCostPerBird)} posted to finance.";
        TempData["Success"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ───────────────────────────── EDIT ─────────────────────────────
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Edit(int id)
    {
        var batch = await batchService.GetByIdAsync(id);
        if (batch == null) return NotFound();
        ViewData["Title"] = $"Edit Flock {batch.BatchCode}";
        ViewData["Subtitle"] = "Correct flock details, counts and status";
        return View(batch);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Writers)]
    public async Task<IActionResult> Edit(int id, Batch form)
    {
        var batch = await batchService.GetByIdAsync(id);
        if (batch == null) return NotFound();
        ModelState.Clear(); // the posted Batch only carries editable fields; validate them explicitly below

        form.BatchCode = (form.BatchCode ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(form.BatchCode)) ModelState.AddModelError(nameof(form.BatchCode), "Batch code is required.");
        else if (await batchService.CodeExistsAsync(form.BatchCode, id))
            ModelState.AddModelError(nameof(form.BatchCode), $"Batch code {form.BatchCode} is used by another flock.");
        if (form.CurrentCount < 0 || form.CurrentCount > batch.InitialCount)
            ModelState.AddModelError(nameof(form.CurrentCount), $"Live birds must be between 0 and {batch.InitialCount:N0}.");
        if (form.MortalityCount < 0 || form.CullingCount < 0)
            ModelState.AddModelError(nameof(form.MortalityCount), "Counts cannot be negative.");

        // keep only the editable fields; the rest stays as stored
        batch.BatchCode = form.BatchCode;
        batch.Breed = string.IsNullOrWhiteSpace(form.Breed) ? batch.Breed : form.Breed.Trim();
        batch.BatchType = BatchTypes.Contains(form.BatchType) ? form.BatchType : batch.BatchType;
        batch.ShedNo = form.ShedNo;
        batch.HatcheryName = form.HatcheryName;
        batch.ChickCostPerBird = Math.Max(0, form.ChickCostPerBird);
        batch.ExpectedHarvestDate = form.ExpectedHarvestDate;
        batch.HealthStatus = string.IsNullOrWhiteSpace(form.HealthStatus) ? batch.HealthStatus : form.HealthStatus;
        batch.CurrentCount = form.CurrentCount;
        batch.MortalityCount = form.MortalityCount;
        batch.CullingCount = form.CullingCount;
        batch.AverageWeightKg = Math.Max(0, form.AverageWeightKg);
        batch.TotalFeedKg = Math.Max(0, form.TotalFeedKg);
        batch.Notes = form.Notes;

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = $"Edit Flock {batch.BatchCode}";
            ViewData["Subtitle"] = "Correct flock details, counts and status";
            return View(batch);
        }

        await batchService.UpdateAsync(batch);
        await batchService.RecalcFarmStockAsync(batch.FarmId);
        TempData["Success"] = $"Flock {batch.BatchCode} updated.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ───────────────────────────── CLOSE / HARVEST ─────────────────────────────
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Close(int id)
    {
        var b = await batchService.GetPerformanceAsync(id);
        if (b == null) return NotFound();
        if (!b.IsActive)
        {
            TempData["Error"] = $"Flock {b.BatchCode} is already closed.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        var vm = ToCloseVm(b);
        vm.HarvestedCount = b.CurrentCount;
        vm.AvgWeightKg = b.AverageWeightKg;
        ViewData["Title"] = $"Close / Harvest {b.BatchCode}";
        ViewData["Subtitle"] = "Record the final harvest and close the flock";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Writers)]
    public async Task<IActionResult> Close(int id, BatchCloseVm form)
    {
        var b = await batchService.GetPerformanceAsync(id);
        if (b == null) return NotFound();
        if (!b.IsActive)
        {
            TempData["Error"] = $"Flock {b.BatchCode} is already closed.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        ModelState.Clear(); // display-only fields of the VM are not posted
        if (form.AvgWeightKg <= 0 || form.AvgWeightKg > 20)
            ModelState.AddModelError(nameof(form.AvgWeightKg), "Enter the average live weight in kg (e.g. 2.15).");
        if (form.HarvestedCount < 0)
            ModelState.AddModelError(nameof(form.HarvestedCount), "Harvested count cannot be negative.");
        if (form.HarvestedCount > b.CurrentCount)
            ModelState.AddModelError(nameof(form.HarvestedCount), $"Cannot harvest more than the {b.CurrentCount:N0} live birds.");
        if (form.HarvestDate.Date < b.PlacementDate.Date)
            ModelState.AddModelError(nameof(form.HarvestDate), "Harvest date cannot be before placement.");
        if (form.HarvestDate.Date > DateTime.Today)
            ModelState.AddModelError(nameof(form.HarvestDate), "Harvest date cannot be in the future.");

        var vm = ToCloseVm(b);
        vm.HarvestDate = form.HarvestDate;
        vm.HarvestedCount = form.HarvestedCount;
        vm.AvgWeightKg = form.AvgWeightKg;
        vm.Notes = form.Notes;
        vm.CreateSalesOrder = form.CreateSalesOrder;

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = $"Close / Harvest {b.BatchCode}";
            ViewData["Subtitle"] = "Record the final harvest and close the flock";
            return View(vm);
        }

        await batchService.CloseAsync(vm);
        TempData["Success"] = $"Flock {b.BatchCode} closed: {vm.HarvestedCount:N0} birds, {Fmt.Num(vm.HarvestedCount * vm.AvgWeightKg, 0)} kg live weight harvested.";
        if (vm.CreateSalesOrder && User.IsAny(Roles.Finance))   // Market/Create is Finance-only
            return Redirect($"/Market/Create?farmId={b.FarmId}&batchId={b.Id}");
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ───────────────────────────── helpers ─────────────────────────────
    private static BatchCloseVm ToCloseVm(Batch b) => new()
    {
        BatchId = b.Id, BatchCode = b.BatchCode, FarmId = b.FarmId, FarmName = b.FarmName, Breed = b.Breed,
        BatchType = b.BatchType, CurrentCount = b.CurrentCount, AgeInDays = b.AgeInDays, TotalFeedKg = b.TotalFeedKg,
        LivabilityPct = b.LivabilityPct, PlacementDate = b.PlacementDate
    };

    private async Task LoadFormListsAsync()
    {
        ViewBag.Farms = (await farmService.GetAllAsync()).Where(f => f.IsActive).ToList();
        ViewBag.Templates = await vaccineService.GetTemplatesAsync(null, true);
    }
}
