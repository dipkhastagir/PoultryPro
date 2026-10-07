using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

/// <summary>IoT sensor network: device registry, live status, telemetry charts, CSV export and integration guide.</summary>
public class SensorsController(ISensorService sensors, IFarmService farmService) : Controller
{
    private static readonly int[] AllowedHours = { 24, 72, 168 };

    public async Task<IActionResult> Index(int? farmId)
    {
        var (warn, crit, nh3) = await sensors.ThresholdsAsync();
        var vm = new SensorIndexVm
        {
            FarmId = farmId,
            Cards = await sensors.GetDeviceCardsAsync(farmId),
            Farms = (await farmService.GetAllAsync()).ToList(),
            ThiWarning = warn, ThiCritical = crit, AmmoniaLimit = nh3,
            ReadingsLast24h = await sensors.CountReadingsSinceAsync(DateTime.Now.AddHours(-24))
        };
        ViewData["Title"] = "IoT Sensors";
        ViewData["Subtitle"] = "Shed climate nodes, connectivity and live environmental readings";
        return View(vm);
    }

    public async Task<IActionResult> Readings(int? farmId, int? deviceId, int hours = 24)
    {
        if (!AllowedHours.Contains(hours)) hours = 24;
        var devices = await sensors.GetDevicesAsync();
        if (deviceId.HasValue)
        {
            var dev = devices.FirstOrDefault(d => d.Id == deviceId);
            if (dev == null) return NotFound();
            farmId ??= dev.FarmId;
            if (dev.FarmId != farmId) deviceId = null;
        }

        var (warn, crit, nh3) = await sensors.ThresholdsAsync();
        var readings = await sensors.GetReadingsAsync(farmId, deviceId, DateTime.Now.AddHours(-hours));
        var vm = new SensorReadingsVm
        {
            FarmId = farmId, DeviceId = deviceId, Hours = hours,
            Farms = (await farmService.GetAllAsync()).ToList(),
            Devices = devices,
            Readings = readings,
            ThiWarning = warn, ThiCritical = crit, AmmoniaLimit = nh3
        };
        vm.Stats = new List<SensorMetricStat>
        {
            Stat("Temperature", "°C", "bi-thermometer-half", readings.Select(r => r.TemperatureC), null),
            Stat("Humidity", "%", "bi-droplet-half", readings.Select(r => r.HumidityPercent), null),
            Stat("THI", "", "bi-sun", readings.Select(r => r.Thi), warn),
            Stat("Ammonia (NH₃)", "ppm", "bi-wind", readings.Select(r => r.AmmoniaPpm), nh3),
            Stat("CO₂", "ppm", "bi-cloud-haze2", readings.Select(r => r.Co2Ppm), 3000),
            Stat("Light", "lux", "bi-lightbulb", readings.Select(r => r.LightLux), null),
            Stat("Water flow", "L", "bi-moisture", readings.Select(r => r.WaterFlowLtr), null),
            Stat("Feed level", "%", "bi-basket", readings.Select(r => r.FeedLevelPct), null),
        };

        var scope = deviceId.HasValue ? devices.First(d => d.Id == deviceId).DeviceCode
                  : farmId.HasValue ? vm.Farms.FirstOrDefault(f => f.Id == farmId)?.Name ?? "Farm"
                  : "All farms";
        ViewData["Title"] = "Sensor Readings";
        ViewData["Subtitle"] = $"{scope} · last {(hours == 168 ? "7 days" : hours + " hours")}";
        return View(vm);
    }

    private static SensorMetricStat Stat(string name, string unit, string icon, IEnumerable<decimal?> values, decimal? limit)
    {
        var v = values.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return new SensorMetricStat
        {
            Metric = name, Unit = unit, Icon = icon, Samples = v.Count, Limit = limit,
            Min = v.Count > 0 ? v.Min() : null,
            Avg = v.Count > 0 ? Math.Round(v.Average(), 2) : null,
            Max = v.Count > 0 ? v.Max() : null,
            Breaches = limit.HasValue ? v.Count(x => x >= limit.Value) : 0
        };
    }

    public async Task<IActionResult> Export(int? farmId, int? deviceId, int hours = 24)
    {
        if (!AllowedHours.Contains(hours)) hours = 24;
        var (warn, crit, _) = await sensors.ThresholdsAsync();
        var rows = (await sensors.GetReadingsAsync(farmId, deviceId, DateTime.Now.AddHours(-hours)))
            .Select(r => new
            {
                r.ReadingTime, Farm = r.FarmName, r.DeviceCode, Shed = r.ShedNo,
                r.TemperatureC, r.HumidityPercent, THI = r.Thi,
                HeatBand = r.Thi.HasValue ? PoultryMath.HeatBand(r.Thi.Value, warn, crit) : "",
                r.AmmoniaPpm, r.Co2Ppm, r.LightLux, r.WaterFlowLtr, r.FeedLevelPct
            });
        return Csv.File(rows, $"sensor-readings-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    public async Task<IActionResult> ApiDocs()
    {
        var devices = await sensors.GetDevicesAsync();
        var vm = new SensorApiDocsVm
        {
            BaseUrl = $"{Request.Scheme}://{Request.Host}",
            Devices = devices,
            Example = devices.FirstOrDefault(d => d.IsActive) ?? devices.FirstOrDefault(),
            CanSimulate = User.IsAny(Roles.AdminOrManager)
        };
        ViewData["Title"] = "Sensor Integration Guide";
        ViewData["Subtitle"] = "Connect ESP32 / Arduino nodes and the mobile app to PoultryPro";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Simulate(int deviceId, int count = 1, string? returnTo = null)
    {
        var device = await sensors.GetDeviceAsync(deviceId);
        if (device == null) return NotFound();
        if (!device.IsActive)
        {
            TempData["Error"] = $"{device.DeviceCode} is deactivated — activate it before sending readings.";
        }
        else
        {
            count = Math.Clamp(count, 1, 12);
            for (var i = 0; i < count; i++) await sensors.SimulateReadingAsync(deviceId);
            var last = await sensors.GetLatestReadingAsync(deviceId);
            TempData["Success"] = $"Test reading{(count > 1 ? "s" : "")} stored for {device.DeviceCode}: " +
                                  $"{last?.TemperatureC:0.0} °C, {last?.HumidityPercent:0} % RH, THI {last?.Thi:0.0}.";
        }
        return returnTo == "readings"
            ? RedirectToAction(nameof(Readings), new { deviceId })
            : returnTo == "index" ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(ApiDocs));
    }

    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Create(int? farmId)
    {
        ViewBag.Farms = (await farmService.GetAllAsync()).ToList();
        ViewData["Title"] = "Register Sensor";
        ViewData["Subtitle"] = "Add an IoT node; an API key is generated automatically";
        var n = (await sensors.GetDevicesAsync()).Count + 1;
        return View("Form", new SensorDeviceFormVm { FarmId = farmId ?? 0, DeviceCode = $"PP-NODE-{n:000}", ShedNo = "S-1" });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Create(SensorDeviceFormVm vm)
    {
        await ValidateAsync(vm);
        if (!ModelState.IsValid)
        {
            ViewBag.Farms = (await farmService.GetAllAsync()).ToList();
            ViewData["Title"] = "Register Sensor";
            ViewData["Subtitle"] = "Add an IoT node; an API key is generated automatically";
            return View("Form", vm);
        }
        var id = await sensors.CreateDeviceAsync(vm);
        TempData["Success"] = $"Sensor {vm.DeviceCode} registered. Copy its API key into the device firmware.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Edit(int id)
    {
        var d = await sensors.GetDeviceAsync(id);
        if (d == null) return NotFound();
        ViewBag.Farms = (await farmService.GetAllAsync()).ToList();
        ViewBag.Device = d;
        ViewData["Title"] = $"Edit Sensor — {d.DeviceCode}";
        ViewData["Subtitle"] = $"{d.FarmName} · {d.ShedNo ?? "no shed"} · {d.ReadingCount:N0} readings stored";
        return View("Form", new SensorDeviceFormVm
        {
            Id = d.Id, DeviceCode = d.DeviceCode, Name = d.Name, FarmId = d.FarmId, ShedNo = d.ShedNo,
            DeviceType = d.DeviceType, FirmwareVersion = d.FirmwareVersion, IsActive = d.IsActive, ApiKey = d.ApiKey, LastSeenAt = d.LastSeenAt
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Edit(SensorDeviceFormVm vm)
    {
        var existing = await sensors.GetDeviceAsync(vm.Id);
        if (existing == null) return NotFound();
        await ValidateAsync(vm);
        if (!ModelState.IsValid)
        {
            ViewBag.Farms = (await farmService.GetAllAsync()).ToList();
            ViewBag.Device = existing;
            vm.ApiKey = existing.ApiKey; vm.LastSeenAt = existing.LastSeenAt;
            ViewData["Title"] = $"Edit Sensor — {existing.DeviceCode}";
            ViewData["Subtitle"] = existing.FarmName;
            return View("Form", vm);
        }
        await sensors.UpdateDeviceAsync(vm);
        TempData["Success"] = $"Sensor {vm.DeviceCode} updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> RegenerateKey(int id)
    {
        var d = await sensors.GetDeviceAsync(id);
        if (d == null) return NotFound();
        await sensors.RegenerateKeyAsync(id);
        TempData["Success"] = $"New API key generated for {d.DeviceCode}. The old key stops working immediately — update the firmware.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var d = await sensors.GetDeviceAsync(id);
        if (d == null) return NotFound();
        await sensors.DeleteDeviceAsync(id);
        TempData["Success"] = $"Sensor {d.DeviceCode} and its {d.ReadingCount:N0} readings were deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(SensorDeviceFormVm vm)
    {
        ModelState.Remove(nameof(SensorDeviceFormVm.ApiKey));
        if (string.IsNullOrWhiteSpace(vm.DeviceCode)) ModelState.AddModelError(nameof(vm.DeviceCode), "Device code is required.");
        else if (vm.DeviceCode.Trim().Length > 40) ModelState.AddModelError(nameof(vm.DeviceCode), "Device code is limited to 40 characters.");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(vm.DeviceCode.Trim(), "^[A-Za-z0-9._-]+$"))
            ModelState.AddModelError(nameof(vm.DeviceCode), "Use letters, digits, dot, dash or underscore only (it is used in URLs).");
        else if (await sensors.DeviceCodeExistsAsync(vm.DeviceCode.Trim(), vm.Id))
            ModelState.AddModelError(nameof(vm.DeviceCode), "Another device already uses this code.");
        if (string.IsNullOrWhiteSpace(vm.Name)) ModelState.AddModelError(nameof(vm.Name), "Name is required.");
        if (vm.FarmId <= 0 || await farmService.GetByIdAsync(vm.FarmId) == null) ModelState.AddModelError(nameof(vm.FarmId), "Select the farm where the device is installed.");
    }
}
