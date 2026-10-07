using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PoultryPro.Infrastructure;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

/// <summary>
/// REST API for the field-officer mobile app. Every request must carry the shared key from the
/// <c>Api.MobileSyncKey</c> setting in the <c>X-Api-Key</c> header.
/// </summary>
[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("api/mobile")]
[Produces("application/json")]
public class MobileApiController(
    IMobileSyncService mobile,
    IVisitService visitService,
    IDailyRecordService dailyRecordService,
    ISettingsService settings) : ControllerBase, IAsyncActionFilter
{
    // ── API-key gate (runs before every action of this controller) ──────────
    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var expected = await settings.GetAsync("Api.MobileSyncKey", "");
        var supplied = Request.Headers["X-Api-Key"].ToString().Trim();
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied)))
        {
            context.Result = Unauthorized(new { ok = false, error = "Missing or invalid X-Api-Key header." });
            return;
        }
        await next();
    }

    /// <summary>POST api/mobile/login — verifies officer credentials, returns profile + assigned farms.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] MobileLoginRequest? body)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrEmpty(body.Password))
            return BadRequest(new { ok = false, error = "username and password are required." });

        var officer = await mobile.GetOfficerByUsernameAsync(body.Username.Trim());
        if (officer == null || !PasswordHasher.Verify(body.Password, officer.PasswordHash))
            return Unauthorized(new { ok = false, error = "Invalid username or password." });
        if (!officer.IsActive)
            return StatusCode(StatusCodes.Status403Forbidden, new { ok = false, error = "This field officer account is inactive." });

        await mobile.TouchLoginAsync(officer.Id);
        var farms = await mobile.GetAssignedFarmsAsync(officer.Id);
        return Ok(new
        {
            ok = true,
            officer = new
            {
                officer.Id, officer.FullName, officer.Username, officer.Phone, officer.Email,
                officer.Zone, officer.Designation, lastLoginAt = DateTime.Now
            },
            farms,
            serverTime = DateTime.Now
        });
    }

    /// <summary>GET api/mobile/officers/{id}/farms — assigned farms with their active flocks.</summary>
    [HttpGet("officers/{id:int}/farms")]
    public async Task<IActionResult> Farms(int id)
    {
        var officer = await mobile.GetOfficerAsync(id);
        if (officer == null) return NotFound(new { ok = false, error = "Field officer not found." });
        if (!officer.IsActive) return StatusCode(StatusCodes.Status403Forbidden, new { ok = false, error = "Field officer is inactive." });
        var farms = await mobile.GetAssignedFarmsAsync(id);
        return Ok(new { ok = true, officerId = id, count = farms.Count, farms });
    }

    /// <summary>POST api/mobile/visits — starts a visit with GPS check-in; returns the new visit id.</summary>
    [HttpPost("visits")]
    public async Task<IActionResult> CreateVisit([FromBody] MobileVisitRequest? body)
    {
        if (body == null) return BadRequest(new { ok = false, error = "Request body is required." });
        var errors = new List<string>();
        if (body.FieldOfficerId <= 0) errors.Add("fieldOfficerId is required.");
        if (body.FarmId <= 0) errors.Add("farmId is required.");
        if (body.Latitude is < -90 or > 90) errors.Add("latitude must be between -90 and 90.");
        if (body.Longitude is < -180 or > 180) errors.Add("longitude must be between -180 and 180.");
        if (body.Latitude.HasValue != body.Longitude.HasValue) errors.Add("latitude and longitude must be supplied together.");
        if (body.GpsAccuracyMeters is < 0) errors.Add("gpsAccuracyMeters cannot be negative.");
        if (body.VisitDate.HasValue && body.VisitDate.Value.Date > DateTime.Today.AddDays(1)) errors.Add("visitDate cannot be in the future.");
        if (errors.Count > 0) return BadRequest(new { ok = false, error = "Validation failed.", details = errors });

        var officer = await mobile.GetOfficerAsync(body.FieldOfficerId);
        if (officer == null) return NotFound(new { ok = false, error = "Field officer not found." });
        if (!officer.IsActive) return StatusCode(StatusCodes.Status403Forbidden, new { ok = false, error = "Field officer is inactive." });
        if (!await mobile.FarmExistsAsync(body.FarmId)) return NotFound(new { ok = false, error = "Farm not found." });
        if (!await mobile.IsFarmAssignedAsync(body.FieldOfficerId, body.FarmId))
            return StatusCode(StatusCodes.Status403Forbidden, new { ok = false, error = "This farm is not assigned to the field officer." });
        if (body.BatchId.HasValue)
        {
            var batch = await mobile.GetBatchAsync(body.BatchId.Value);
            if (batch == null) return NotFound(new { ok = false, error = "Batch not found." });
            if (batch.Value.FarmId != body.FarmId) return BadRequest(new { ok = false, error = "Batch does not belong to the selected farm." });
        }

        var id = await mobile.CreateVisitAsync(body);
        return StatusCode(StatusCodes.Status201Created, new { ok = true, id, status = "In Progress", syncStatus = "Pending" });
    }

    /// <summary>POST api/mobile/visits/{id}/daily-records — adds a daily flock record (updates batch statistics).</summary>
    [HttpPost("visits/{id:int}/daily-records")]
    public async Task<IActionResult> AddDailyRecord(int id, [FromBody] MobileDailyRecordRequest? body)
    {
        if (body == null) return BadRequest(new { ok = false, error = "Request body is required." });
        var visit = await mobile.GetVisitAsync(id);
        if (visit == null) return NotFound(new { ok = false, error = "Visit not found." });

        var batchId = body.BatchId ?? visit.Value.BatchId;
        var errors = new List<string>();
        if (!batchId.HasValue || batchId <= 0) errors.Add("batchId is required (the visit has no batch).");
        if (body.BirdCount < 0) errors.Add("birdCount cannot be negative.");
        if (body.MortalityCount < 0) errors.Add("mortalityCount cannot be negative.");
        if (body.CullingCount < 0) errors.Add("cullingCount cannot be negative.");
        if (body.FeedConsumptionKg < 0) errors.Add("feedConsumptionKg cannot be negative.");
        if (body.WaterConsumptionLtr < 0) errors.Add("waterConsumptionLtr cannot be negative.");
        if (body.HumidityPercent is < 0 or > 100) errors.Add("humidityPercent must be between 0 and 100.");
        if (body.TemperatureC is < -20 or > 70) errors.Add("temperatureC must be between -20 and 70.");
        if (body.LightHours is < 0 or > 24) errors.Add("lightHours must be between 0 and 24.");
        if (body.AverageWeightGm is < 0 or > 10000) errors.Add("averageWeightGm must be between 0 and 10000.");
        if (body.RecordDate.HasValue && body.RecordDate.Value.Date > DateTime.Today.AddDays(1)) errors.Add("recordDate cannot be in the future.");
        if (errors.Count > 0) return BadRequest(new { ok = false, error = "Validation failed.", details = errors });

        var batch = await mobile.GetBatchAsync(batchId!.Value);
        if (batch == null) return NotFound(new { ok = false, error = "Batch not found." });
        if (batch.Value.FarmId != visit.Value.FarmId) return BadRequest(new { ok = false, error = "Batch does not belong to the visit's farm." });
        if (!batch.Value.IsActive) return BadRequest(new { ok = false, error = "Batch is closed; daily records can no longer be added." });

        var vm = new DailyRecordCreateVm
        {
            VisitId = id, BatchId = batchId.Value, FarmId = visit.Value.FarmId,
            RecordDate = (body.RecordDate ?? DateTime.Today).Date,
            BirdCount = body.BirdCount, MortalityCount = body.MortalityCount, CullingCount = body.CullingCount,
            FeedConsumptionKg = body.FeedConsumptionKg, WaterConsumptionLtr = body.WaterConsumptionLtr,
            TemperatureC = body.TemperatureC, HumidityPercent = body.HumidityPercent, LightHours = body.LightHours,
            AverageWeightGm = body.AverageWeightGm, MedicineUsed = Clean(body.MedicineUsed, 150),
            MedicineDosage = Clean(body.MedicineDosage, 100), VaccineUsed = Clean(body.VaccineUsed, 150), Remarks = Clean(body.Remarks, 500)
        };
        var recordId = await dailyRecordService.CreateAsync(vm);
        decimal? thi = vm.TemperatureC.HasValue && vm.HumidityPercent.HasValue ? PoultryMath.THI(vm.TemperatureC.Value, vm.HumidityPercent.Value) : null;
        return StatusCode(StatusCodes.Status201Created, new { ok = true, id = recordId, visitId = id, batchId = vm.BatchId, thi });
    }

    /// <summary>POST api/mobile/visits/{id}/complete — marks the visit Completed and stamps the check-out time.</summary>
    [HttpPost("visits/{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, [FromBody] MobileCompleteRequest? body)
    {
        var visit = await mobile.GetVisitAsync(id);
        if (visit == null) return NotFound(new { ok = false, error = "Visit not found." });
        if (visit.Value.VisitStatus == "Cancelled") return BadRequest(new { ok = false, error = "Cancelled visits cannot be completed." });
        await mobile.CompleteVisitAsync(id, Clean(body?.Remark, 1000));
        return Ok(new { ok = true, id, status = "Completed", checkOutTime = DateTime.Now });
    }

    /// <summary>POST api/mobile/visits/{id}/sync — marks the visit and all its child records as synced.</summary>
    [HttpPost("visits/{id:int}/sync")]
    public async Task<IActionResult> Sync(int id)
    {
        var visit = await mobile.GetVisitAsync(id);
        if (visit == null) return NotFound(new { ok = false, error = "Visit not found." });
        await visitService.MarkSyncedAsync(id);
        return Ok(new { ok = true, id, syncStatus = "Synced", syncedAt = DateTime.Now });
    }

    private static string? Clean(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length > max ? s[..max] : s;
    }
}
