using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

[Authorize(Roles = Roles.Finance + "," + Roles.Viewer)]
public class SupplierController(ISupplierService supplierService, IPurchaseService purchaseService) : Controller
{
    public static readonly string[] Categories =
        { "Feed", "Medicine", "Vaccine", "Supplement", "Equipment", "Biosecurity", "Day-old Chicks", "Packaging", "Services", "General" };

    public async Task<IActionResult> Index(string? category, bool inactive = false, string? sort = null)
    {
        var suppliers = (await supplierService.GetAllAsync(includeInactive: true)).ToList();
        var perf = (await supplierService.GetPerformanceAsync()).ToDictionary(p => p.Id);
        var itemCounts = await supplierService.GetItemCountsAsync();

        var allCats = suppliers.Select(s => s.Category).Union(Categories).Distinct().OrderBy(c => c).ToList();
        var filtered = suppliers.Where(s => inactive || s.IsActive);
        if (!string.IsNullOrEmpty(category)) filtered = filtered.Where(s => s.Category == category);

        var cards = filtered.Select(s => new SupplierCardVm
        {
            Supplier = s,
            Perf = perf.GetValueOrDefault(s.Id),
            ItemCount = itemCounts.GetValueOrDefault(s.Id)
        });
        cards = sort switch
        {
            "spend"  => cards.OrderByDescending(c => c.Perf?.TotalSpend ?? 0),
            "rating" => cards.OrderByDescending(c => c.Supplier.Rating),
            "ontime" => cards.OrderByDescending(c => c.Perf?.OnTimePct ?? -1),
            _        => cards.OrderByDescending(c => c.Supplier.IsActive).ThenBy(c => c.Supplier.Name)
        };

        var active = suppliers.Where(s => s.IsActive).ToList();
        var activePerf = perf.Values.Where(p => active.Any(a => a.Id == p.Id)).ToList();
        ViewBag.Categories = allCats;
        ViewBag.Category = category;
        ViewBag.Inactive = inactive;
        ViewBag.Sort = sort;
        ViewBag.ActiveCount = active.Count;
        ViewBag.InactiveCount = suppliers.Count - active.Count;
        ViewBag.TotalSpend = activePerf.Sum(p => p.TotalSpend);
        ViewBag.AvgRating = active.Count > 0 ? active.Average(s => s.Rating) : 0m;
        var received = activePerf.Sum(p => p.ReceivedOrders);
        ViewBag.OnTimePct = received > 0 ? activePerf.Sum(p => p.OnTimeOrders) * 100m / received : (decimal?)null;
        ViewBag.SpendByCategory = activePerf.GroupBy(p => p.Category)
            .Select(g => (Category: g.Key, Spend: g.Sum(p => p.TotalSpend)))
            .Where(x => x.Spend > 0).OrderByDescending(x => x.Spend).ToList();

        ViewData["Title"] = "Suppliers";
        ViewData["Subtitle"] = "Vendor directory, ratings, spend and delivery performance";
        return View(cards.ToList());
    }

    public async Task<IActionResult> Detail(int id)
    {
        var s = await supplierService.GetByIdAsync(id);
        if (s == null) return NotFound();
        var orders = (await purchaseService.GetAllAsync(supplierId: id)).ToList();
        var received = orders.Where(o => o.LeadTimeDays.HasValue).ToList();
        var vm = new SupplierDetailVm
        {
            Supplier = s,
            Perf = await supplierService.GetPerformanceAsync(id) ?? new SupplierPerformance { Id = id, Name = s.Name, Category = s.Category, Rating = s.Rating },
            Orders = orders,
            Items = (await supplierService.GetItemsAsync(id)).ToList(),
            OpenOrderValue = orders.Where(o => o.Status is "Draft" or "Ordered").Sum(o => o.TotalAmount),
            UnpaidValue = orders.Where(o => o.Status == "Received" && o.PaymentStatus != "Paid").Sum(o => o.TotalAmount),
            AvgLeadTimeDays = received.Count > 0 ? (decimal)received.Average(o => o.LeadTimeDays!.Value) : null
        };
        var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
        for (var m = start; m <= DateTime.Today; m = m.AddMonths(1))
        {
            var mm = m;
            vm.MonthlySpend.Add((mm.ToString("MMM yy"), orders
                .Where(o => o.Status != "Cancelled" && o.OrderDate.Year == mm.Year && o.OrderDate.Month == mm.Month)
                .Sum(o => o.TotalAmount)));
        }
        ViewData["Title"] = s.Name;
        ViewData["Subtitle"] = $"{s.Category} supplier · {(s.IsActive ? "Active" : "Inactive")}";
        return View(vm);
    }

    [Authorize(Roles = Roles.Finance)]
    public IActionResult Create()
    {
        ViewData["Title"] = "Add Supplier";
        ViewData["Subtitle"] = "Register a new vendor";
        ViewBag.Categories = Categories;
        return View("Form", new Supplier { Rating = 4.0m, IsActive = true });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Create(Supplier s)
    {
        Validate(s);
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Add Supplier";
            ViewData["Subtitle"] = "Register a new vendor";
            ViewBag.Categories = Categories;
            return View("Form", s);
        }
        var id = await supplierService.CreateAsync(s);
        TempData["Success"] = $"Supplier \"{s.Name}\" added.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Edit(int id)
    {
        var s = await supplierService.GetByIdAsync(id);
        if (s == null) return NotFound();
        ViewData["Title"] = "Edit Supplier";
        ViewData["Subtitle"] = s.Name;
        ViewBag.Categories = Categories;
        return View("Form", s);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Edit(Supplier s)
    {
        Validate(s);
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit Supplier";
            ViewData["Subtitle"] = s.Name;
            ViewBag.Categories = Categories;
            return View("Form", s);
        }
        await supplierService.UpdateAsync(s);
        TempData["Success"] = $"Supplier \"{s.Name}\" updated.";
        return RedirectToAction(nameof(Detail), new { id = s.Id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await supplierService.GetByIdAsync(id);
        if (s == null) return NotFound();
        await supplierService.SetActiveAsync(id, false);
        TempData["Success"] = $"Supplier \"{s.Name}\" deactivated. Purchase history is preserved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Reactivate(int id)
    {
        await supplierService.SetActiveAsync(id, true);
        TempData["Success"] = "Supplier reactivated.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    private void Validate(Supplier s)
    {
        ModelState.KeepPostedOnly(Request.Form);
        if (string.IsNullOrWhiteSpace(s.Name)) ModelState.AddModelError(nameof(s.Name), "Supplier name is required.");
        if (s.Rating < 0 || s.Rating > 5) ModelState.AddModelError(nameof(s.Rating), "Rating must be between 0 and 5.");
        if (!string.IsNullOrWhiteSpace(s.Email) && !s.Email.Contains('@')) ModelState.AddModelError(nameof(s.Email), "Enter a valid email address.");
        if (string.IsNullOrWhiteSpace(s.Category)) s.Category = "General";
    }
}
