using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;


namespace PoultryPro.Controllers;

public class VisitController(
    IVisitService visitService,
    IBatchService batchService,
    IFarmService farmService,
    IFieldOfficerService fieldOfficerService,
    IDailyRecordService dailyRecordService,
    IWebHostEnvironment env) : Controller
{
    // ── INDEX ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index(int? farmId, int? officerId, string? status, bool all = false)
    {
        // Field officers land on their own visits unless they explicitly ask for everyone's
        var myOfficerId = User.FieldOfficerId();
        var mineOnly = myOfficerId.HasValue && !officerId.HasValue && !all;
        if (mineOnly) officerId = myOfficerId;

        var visits = await visitService.GetAllAsync(farmId, officerId);

        if (!string.IsNullOrEmpty(status))
            visits = visits.Where(v => v.VisitStatus == status);

        ViewBag.Farms    = await farmService.GetAllAsync();
        ViewBag.Officers = await fieldOfficerService.GetAllAsync();
        ViewBag.SelectedFarm    = farmId;
        ViewBag.SelectedOfficer = officerId;
        ViewBag.SelectedStatus  = status;
        ViewBag.MineOnly        = mineOnly;
        ViewBag.MyOfficerId     = myOfficerId;
        ViewBag.TotalVisits     = visits.Count();
        ViewBag.PendingSync     = visits.Count(v => v.SyncStatus == "Pending");

        ViewData["Title"]    = mineOnly ? "My Farm Visits" : "Farm Visits";
        ViewData["Subtitle"] = "GPS check-ins, photos and daily entries";
        return View(visits.ToList());
    }

    // ── DETAIL ────────────────────────────────────────────────────────────
    public async Task<IActionResult> Detail(int id)
    {
        var vm = await visitService.GetDetailAsync(id);
        if (vm.Visit.Id == 0) return NotFound();

        ViewData["Title"]    = $"Visit — {vm.Visit.FarmName}";
        ViewData["Subtitle"] = $"{vm.Visit.FieldOfficerName} · {vm.Visit.VisitDate:dd MMM yyyy}";
        return View(vm);
    }

    // ── CREATE ────────────────────────────────────────────────────────────
    [Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> Create(int? farmId, int? batchId)
    {
        ViewBag.Farms    = await farmService.GetAllAsync();
        ViewBag.Officers = (await fieldOfficerService.GetAllAsync()).Where(o => o.IsActive);

        IEnumerable<Batch> batches = new List<Batch>();
        if (farmId.HasValue)
            batches = await batchService.GetAllByFarmAsync(farmId.Value);
        ViewBag.Batches = batches;

        var vm = new VisitCreateVm
        {
            FarmId    = farmId ?? 0,
            BatchId   = batchId,
            VisitDate = DateTime.Today
        };
        ViewData["Title"] = "New Farm Visit";
        ViewData["Subtitle"] = "Record a field-officer check-in with GPS, then add daily flock entries";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> Create(VisitCreateVm vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Farms    = await farmService.GetAllAsync();
            ViewBag.Officers = await fieldOfficerService.GetAllAsync();
            ViewBag.Batches  = vm.FarmId > 0
                ? await batchService.GetAllByFarmAsync(vm.FarmId)
                : new List<Batch>();
            return View(vm);
        }
        var id = await visitService.CreateAsync(vm);
        TempData["Success"] = "Visit created.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ── EDIT ──────────────────────────────────────────────────────────────
    [Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> Edit(int id)
    {
        var visit = await visitService.GetByIdAsync(id);
        if (visit == null) return NotFound();

        ViewBag.Farms    = await farmService.GetAllAsync();
        ViewBag.Officers = await fieldOfficerService.GetAllAsync();
        ViewBag.Batches  = await batchService.GetAllByFarmAsync(visit.FarmId);

        var vm = new VisitCreateVm
        {
            FieldOfficerId = visit.FieldOfficerId,
            FarmId         = visit.FarmId,
            BatchId        = visit.BatchId,
            VisitDate      = visit.VisitDate,
            VisitPurpose   = visit.VisitPurpose,
            VisitStatus    = visit.VisitStatus,
            CheckInLatitude  = visit.CheckInLatitude,
            CheckInLongitude = visit.CheckInLongitude,
            GpsAccuracyMeters = visit.GpsAccuracyMeters,
            OverallRemark  = visit.OverallRemark
        };
        ViewBag.VisitId   = id;
        ViewData["Title"] = $"Edit Visit #{id}";
        ViewData["Subtitle"] = $"{visit.FarmName} · {visit.VisitDate:dd MMM yyyy}";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> Edit(int visitId, VisitCreateVm vm)
    {
        var visit = await visitService.GetByIdAsync(visitId);
        if (visit == null) return NotFound();
        if (vm.FieldOfficerId <= 0) vm.FieldOfficerId = visit.FieldOfficerId;
        if (vm.FarmId <= 0) vm.FarmId = visit.FarmId;
        if (vm.VisitDate == default) vm.VisitDate = visit.VisitDate;
        if (string.IsNullOrWhiteSpace(vm.VisitPurpose)) vm.VisitPurpose = visit.VisitPurpose;
        if (vm.BatchId.HasValue && (await batchService.GetByIdAsync(vm.BatchId.Value))?.FarmId != vm.FarmId)
            vm.BatchId = null;   // batch list is not reloaded when the farm changes
        await visitService.UpdateDetailsAsync(visitId, vm);
        await visitService.UpdateStatusAsync(visitId, vm.VisitStatus,
            null, vm.CheckInLatitude, vm.CheckInLongitude,
            vm.GpsAccuracyMeters, vm.OverallRemark);
        TempData["Success"] = "Visit updated.";
        return RedirectToAction(nameof(Detail), new { id = visitId });
    }

    // ── UPDATE STATUS (quick) ─────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> UpdateStatus(int id, string status, string? remark)
    {
        await visitService.UpdateStatusAsync(id, status, remark: remark);
        TempData["Success"] = $"Visit status → {status}.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ── DELETE ────────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> Delete(int id)
    {
        await visitService.DeleteAsync(id);
        TempData["Success"] = "Visit deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── UPLOAD PHOTO ──────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> UploadPhoto(int visitId, string photoType,
                                                   string? caption, IFormFile photo)
    {
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
        if (photo != null && photo.Length > 0
            && !allowed.Contains(Path.GetExtension(photo.FileName).ToLowerInvariant()))
        {
            TempData["Error"] = "Only image files (jpg, png, gif, webp, bmp) can be uploaded.";
            return RedirectToAction(nameof(Detail), new { id = visitId });
        }
        if (photo != null && photo.Length > 0)
        {
            var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "photos");
            Directory.CreateDirectory(uploadsDir);

            var safeName = string.Concat(Path.GetFileName(photo.FileName).Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_'));
            var fileName = $"{visitId}_{DateTime.Now:yyyyMMddHHmmss}_{safeName}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await photo.CopyToAsync(stream);

            var sizeKb = (int)(photo.Length / 1024);
            await visitService.AddPhotoAsync(visitId, photoType, fileName,
                $"/uploads/photos/{fileName}", sizeKb, caption);

            TempData["Success"] = "Photo uploaded.";
        }
        return RedirectToAction(nameof(Detail), new { id = visitId });
    }

    // ── DELETE PHOTO ──────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> DeletePhoto(int photoId, int visitId)
    {
        await visitService.DeletePhotoAsync(photoId);
        TempData["Success"] = "Photo deleted.";
        return RedirectToAction(nameof(Detail), new { id = visitId });
    }

    // ── SAVE SIGNATURE ────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> SaveSignature(int visitId, string farmerName,
                                                    string? farmerPhone, IFormFile signatureFile)
    {
        if (signatureFile != null && signatureFile.Length > 0)
        {
            var dir = Path.Combine(env.WebRootPath, "uploads", "signatures");
            Directory.CreateDirectory(dir);
            var fileName = $"sig_{visitId}_{DateTime.Now:yyyyMMddHHmmss}.png";
            var filePath = Path.Combine(dir, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await signatureFile.CopyToAsync(stream);

            await visitService.SaveSignatureAsync(visitId, farmerName,
                farmerPhone, $"/uploads/signatures/{fileName}");
            TempData["Success"] = "Farmer signature saved.";
        }
        return RedirectToAction(nameof(Detail), new { id = visitId });
    }

    // ── MARK SYNCED ───────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> MarkSynced(int id)
    {
        await visitService.MarkSyncedAsync(id);
        TempData["Success"] = "Visit marked as synced.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ── GET BATCHES FOR FARM (AJAX) ────────────────────────────────────────
    public async Task<IActionResult> GetBatchesByFarm(int farmId)
    {
        var batches = await batchService.GetAllByFarmAsync(farmId);
        return Json(batches.Where(b => b.IsActive)
            .Select(b => new { b.Id, b.BatchCode, b.Breed, b.AgeInDays, b.CurrentCount }));
    }
}
