using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;

namespace PoultryPro.Controllers;

public class FarmController(IFarmService farmService) : Controller
{
    public static readonly string[] FarmTypes = { "Broiler", "Sonali", "Layer", "Breeder", "Duck", "Mixed" };

    public async Task<IActionResult> Index(string? type, string? zone)
    {
        var farms = (await farmService.GetAllAsync()).ToList();
        var certs = await farmService.GetCertificationsAsync();
        var bench = (await farmService.GetBenchmarksAsync()).ToDictionary(b => b.FarmId);

        ViewBag.AllZones = farms.Where(f => !string.IsNullOrWhiteSpace(f.Zone)).Select(f => f.Zone!).Distinct().OrderBy(z => z).ToList();
        ViewBag.AllTypes = farms.Select(f => f.FarmType).Distinct().OrderBy(t => t).ToList();
        if (!string.IsNullOrEmpty(type)) farms = farms.Where(f => f.FarmType == type).ToList();
        if (!string.IsNullOrEmpty(zone)) farms = farms.Where(f => f.Zone == zone).ToList();

        ViewBag.Certifications = certs.ToList();
        ViewBag.Benchmarks = bench;
        ViewBag.Type = type;
        ViewBag.Zone = zone;
        ViewData["Title"] = "Farm Management";
        ViewData["Subtitle"] = "Registered farms, capacity, location and certifications";
        return View(farms);
    }

    public async Task<IActionResult> Detail(int id)
    {
        var vm = await farmService.GetDetailAsync(id);
        if (vm == null) return NotFound();
        ViewData["Title"] = vm.Farm.Name;
        ViewData["Subtitle"] = $"{vm.Farm.FarmType} farm · {vm.Farm.Location}{(string.IsNullOrEmpty(vm.Farm.Zone) ? "" : " · " + vm.Farm.Zone)}";
        return View(vm);
    }

    public async Task<IActionResult> Map()
    {
        var vm = await farmService.GetMapAsync(100);
        ViewData["Title"] = "Farm Map";
        ViewData["Subtitle"] = "Geographic view of farms, open alerts, utilization and field-visit GPS check-ins";
        return View(vm);
    }

    [Authorize(Roles = Roles.AdminOrManager)]
    public IActionResult Create()
    {
        ViewData["Title"] = "Add Farm";
        ViewData["Subtitle"] = "Register a new farm with its location and ownership";
        return View(new Farm());
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Create(Farm farm)
    {
        Validate(farm);
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Add Farm";
            ViewData["Subtitle"] = "Register a new farm with its location and ownership";
            return View(farm);
        }
        var id = await farmService.CreateAsync(farm);
        TempData["Success"] = "Farm added successfully.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Edit(int id)
    {
        var farm = await farmService.GetByIdAsync(id);
        if (farm == null) return NotFound();
        ViewData["Title"] = "Edit Farm";
        ViewData["Subtitle"] = farm.Name;
        return View(farm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Edit(Farm farm)
    {
        Validate(farm);
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit Farm";
            ViewData["Subtitle"] = farm.Name;
            return View(farm);
        }
        await farmService.UpdateAsync(farm);
        TempData["Success"] = "Farm updated successfully.";
        return RedirectToAction(nameof(Detail), new { id = farm.Id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        await farmService.DeleteAsync(id);
        TempData["Success"] = "Farm removed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> AddCertification(Certification cert)
    {
        if (string.IsNullOrWhiteSpace(cert.CertName))
            TempData["Error"] = "Certificate name is required.";
        else if (cert.IssuedDate == default || cert.ExpiryDate == default)
            TempData["Error"] = "Issue and expiry dates are required.";
        else if (cert.ExpiryDate < cert.IssuedDate)
            TempData["Error"] = "Expiry date must be after the issue date.";
        else if (await farmService.GetByIdAsync(cert.FarmId) == null)
            return NotFound();
        else
        {
            await farmService.AddCertificationAsync(cert);
            TempData["Success"] = $"Certification '{cert.CertName}' added.";
        }
        return Redirect(Url.Action(nameof(Detail), new { id = cert.FarmId }) + "#certs");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> DeleteCertification(int id)
    {
        var farmId = await farmService.DeleteCertificationAsync(id);
        if (farmId == null) return NotFound();
        TempData["Success"] = "Certification removed.";
        return Redirect(Url.Action(nameof(Detail), new { id = farmId }) + "#certs");
    }

    private void Validate(Farm farm)
    {
        if (string.IsNullOrWhiteSpace(farm.Name)) ModelState.AddModelError(nameof(Farm.Name), "Farm name is required.");
        if (string.IsNullOrWhiteSpace(farm.Location)) ModelState.AddModelError(nameof(Farm.Location), "Location is required.");
        if (farm.Capacity < 0) ModelState.AddModelError(nameof(Farm.Capacity), "Capacity cannot be negative.");
        if (farm.Latitude is < -90 or > 90) ModelState.AddModelError(nameof(Farm.Latitude), "Latitude must be between -90 and 90.");
        if (farm.Longitude is < -180 or > 180) ModelState.AddModelError(nameof(Farm.Longitude), "Longitude must be between -180 and 180.");
        if (farm.Latitude.HasValue != farm.Longitude.HasValue) ModelState.AddModelError(nameof(Farm.Latitude), "Provide both latitude and longitude (or neither).");
        if (farm.ShedCount < 1) ModelState.AddModelError(nameof(Farm.ShedCount), "A farm has at least one shed.");
        if (farm.AreaSqFt is < 0) ModelState.AddModelError(nameof(Farm.AreaSqFt), "Area cannot be negative.");
    }
}
