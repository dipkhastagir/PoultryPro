using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

/// <summary>
/// Telemetry endpoint for IoT nodes (ESP32 etc.). Each device authenticates with its own key in the
/// <c>X-Device-Key</c> header, which must match SensorDevices.ApiKey for the posted deviceCode.
/// </summary>
[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("api/sensors")]
[Produces("application/json")]
public class SensorApiController(ISensorService sensors) : ControllerBase
{
    private const string KeyHeader = "X-Device-Key";

    /// <summary>POST api/sensors/ingest — stores one reading and returns the computed THI and heat-stress band.</summary>
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] SensorIngestRequest? body)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.DeviceCode))
            return BadRequest(new { ok = false, error = "deviceCode is required." });

        var errors = Validate(body);
        if (errors.Count > 0) return BadRequest(new { ok = false, error = "Invalid reading.", details = errors });

        var (device, fail) = await AuthenticateAsync(body.DeviceCode.Trim());
        if (fail != null) return fail;

        if (body.ReadingTime.HasValue && (body.ReadingTime.Value > DateTime.Now.AddMinutes(10) || body.ReadingTime.Value < DateTime.Now.AddDays(-30)))
            return BadRequest(new { ok = false, error = "readingTime must be within the last 30 days and not in the future." });

        var id = await sensors.InsertReadingAsync(device!, body);
        decimal? thi = null; string band = "Unknown";
        if (body.TemperatureC.HasValue && body.HumidityPercent.HasValue)
        {
            var (warn, crit, _) = await sensors.ThresholdsAsync();
            thi = PoultryMath.THI(body.TemperatureC.Value, body.HumidityPercent.Value);
            band = PoultryMath.HeatBand(thi.Value, warn, crit);
        }
        return Ok(new { ok = true, id, deviceCode = device!.DeviceCode, thi, band, receivedAt = DateTime.Now });
    }

    /// <summary>GET api/sensors/{deviceCode}/latest — last stored reading of the device (same key header).</summary>
    [HttpGet("{deviceCode}/latest")]
    public async Task<IActionResult> Latest(string deviceCode)
    {
        var (device, fail) = await AuthenticateAsync(deviceCode.Trim());
        if (fail != null) return fail;

        var r = await sensors.GetLatestReadingAsync(device!.Id);
        if (r == null) return NotFound(new { ok = false, error = "No readings stored for this device yet." });
        var (warn, crit, _) = await sensors.ThresholdsAsync();
        return Ok(new
        {
            ok = true,
            deviceCode = device.DeviceCode,
            farmId = device.FarmId,
            shedNo = device.ShedNo,
            reading = new
            {
                r.Id, r.ReadingTime, r.TemperatureC, r.HumidityPercent, r.AmmoniaPpm, r.Co2Ppm,
                r.LightLux, r.WaterFlowLtr, r.FeedLevelPct
            },
            thi = r.Thi,
            band = r.Thi.HasValue ? PoultryMath.HeatBand(r.Thi.Value, warn, crit) : "Unknown"
        });
    }

    private async Task<(SensorDevice? Device, IActionResult? Fail)> AuthenticateAsync(string deviceCode)
    {
        var key = Request.Headers[KeyHeader].ToString();
        if (string.IsNullOrWhiteSpace(key))
            return (null, Unauthorized(new { ok = false, error = $"Missing {KeyHeader} header." }));

        var device = await sensors.GetDeviceByCodeAsync(deviceCode);
        if (device == null || !FixedEquals(device.ApiKey, key.Trim()))
            return (null, Unauthorized(new { ok = false, error = "Unknown device or invalid device key." }));
        if (!device.IsActive)
            return (null, StatusCode(StatusCodes.Status403Forbidden, new { ok = false, error = "Device is deactivated." }));
        return (device, null);
    }

    private static bool FixedEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a.ToLowerInvariant()), Encoding.UTF8.GetBytes(b.ToLowerInvariant()));

    private static List<string> Validate(SensorIngestRequest r)
    {
        var e = new List<string>();
        void Range(decimal? v, decimal min, decimal max, string name)
        { if (v.HasValue && (v < min || v > max)) e.Add($"{name} must be between {min} and {max}."); }
        Range(r.TemperatureC, -20, 70, "temperatureC");
        Range(r.HumidityPercent, 0, 100, "humidityPercent");
        Range(r.AmmoniaPpm, 0, 500, "ammoniaPpm");
        Range(r.Co2Ppm, 0, 50000, "co2Ppm");
        Range(r.LightLux, 0, 200000, "lightLux");
        Range(r.WaterFlowLtr, 0, 100000, "waterFlowLtr");
        Range(r.FeedLevelPct, 0, 100, "feedLevelPct");
        if (r.TemperatureC == null && r.HumidityPercent == null && r.AmmoniaPpm == null && r.Co2Ppm == null
            && r.LightLux == null && r.WaterFlowLtr == null && r.FeedLevelPct == null)
            e.Add("At least one measurement is required.");
        return e;
    }
}
