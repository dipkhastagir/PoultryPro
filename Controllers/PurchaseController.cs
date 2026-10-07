using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

[Authorize(Roles = Roles.Finance + "," + Roles.Viewer)]
public class PurchaseController(
    IPurchaseService purchaseService,
    ISupplierService supplierService,
    IInventoryService inventoryService,
    IFarmService farmService,
    ISettingsService settings) : Controller
{
    public static readonly string[] Statuses = { "Draft", "Ordered", "Received", "Cancelled" };

    public async Task<IActionResult> Index(string? status, string? q)
    {
        var all = (await purchaseService.GetAllAsync()).ToList();
        var vm = new PurchaseIndexVm
        {
            Status = status,
            Q = q,
            CountByStatus = Statuses.ToDictionary(s => s, s => all.Count(o => o.Status == s)),
            ValueByStatus = Statuses.ToDictionary(s => s, s => all.Where(o => o.Status == s).Sum(o => o.TotalAmount)),
            UnpaidReceived = all.Where(o => o.Status == "Received" && o.PaymentStatus != "Paid").Sum(o => o.TotalAmount),
            OverdueCount = all.Count(o => o.IsOverdue)
        };
        IEnumerable<PurchaseOrder> list = all;
        if (!string.IsNullOrEmpty(status)) list = list.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
            list = list.Where(o => o.PoNumber.Contains(q, StringComparison.OrdinalIgnoreCase)
                                || o.SupplierName.Contains(q, StringComparison.OrdinalIgnoreCase)
                                || (o.Notes ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        vm.Orders = list.ToList();
        ViewData["Title"] = "Purchase Orders";
        ViewData["Subtitle"] = "Procurement workflow: draft → ordered → received → paid";
        return View(vm);
    }

    [Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Create(int? supplierId, int? itemId)
    {
        var vm = new PurchaseOrderCreateVm { SupplierId = supplierId ?? 0 };
        if (itemId.HasValue)
        {
            var item = await inventoryService.GetByIdAsync(itemId.Value);
            if (item != null)
            {
                vm.Lines.Add(new PurchaseLineInput
                {
                    InventoryItemId = item.Id,
                    Quantity = item.SuggestedReorderQty > 0 ? item.SuggestedReorderQty : Math.Max(1, item.MinimumLevel),
                    UnitCost = item.UnitCost
                });
                if (vm.SupplierId == 0 && item.SupplierId.HasValue) vm.SupplierId = item.SupplierId.Value;
                vm.Notes = $"Re-order: {item.Name} is {item.StockStatus.ToLowerInvariant()} ({Fmt.Num(item.Quantity, 2)} {item.Unit} on hand, minimum {Fmt.Num(item.MinimumLevel, 2)}).";
            }
        }
        await FillLookupsAsync();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Create(PurchaseOrderCreateVm vm)
    {
        vm.Lines ??= new();
        ModelState.KeepPostedOnly(Request.Form);
        // blank rows (no item chosen, e.g. an empty quantity box) are ignored below, so drop their binding errors too
        for (var i = 0; i < vm.Lines.Count; i++)
            if (vm.Lines[i].InventoryItemId <= 0)
                foreach (var k in ModelState.Keys.Where(k => k.StartsWith($"Lines[{i}].", StringComparison.OrdinalIgnoreCase)).ToList())
                    ModelState.Remove(k);
        var valid = vm.Lines.Where(l => l.InventoryItemId > 0).ToList();
        if (valid.Count == 0) ModelState.AddModelError("Lines", "Add at least one line item.");
        if (valid.Any(l => l.Quantity <= 0)) ModelState.AddModelError("Lines", "Every line needs a quantity greater than zero.");
        if (valid.Any(l => l.UnitCost < 0)) ModelState.AddModelError("Lines", "Unit cost cannot be negative.");
        if (valid.GroupBy(l => l.InventoryItemId).Any(g => g.Count() > 1)) ModelState.AddModelError("Lines", "Each item can appear only once — combine duplicate lines.");
        if (vm.ExpectedDate.HasValue && vm.ExpectedDate.Value.Date < vm.OrderDate.Date) ModelState.AddModelError(nameof(vm.ExpectedDate), "Expected date cannot be before the order date.");
        var supplier = vm.SupplierId > 0 ? await supplierService.GetByIdAsync(vm.SupplierId) : null;
        if (supplier == null) ModelState.AddModelError(nameof(vm.SupplierId), "Choose a supplier.");
        else if (!supplier.IsActive) ModelState.AddModelError(nameof(vm.SupplierId), "This supplier is inactive.");

        if (!ModelState.IsValid)
        {
            await FillLookupsAsync();
            return View(vm);
        }
        vm.Lines = valid;
        var (id, po) = await purchaseService.CreateAsync(vm, User.UserName());
        TempData["Success"] = $"Purchase order {po} created as {(vm.SaveAs == "Ordered" ? "Ordered" : "Draft")}.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    public async Task<IActionResult> Detail(int id)
    {
        var po = await purchaseService.GetByIdAsync(id);
        if (po == null) return NotFound();
        var vm = new PurchaseDetailVm
        {
            Order = po,
            Lines = (await purchaseService.GetLinesAsync(id)).ToList(),
            CompanyName = await settings.GetAsync("Company.Name", "PoultryPro Integrated Farms Ltd."),
            CompanyAddress = await settings.GetAsync("Company.Address", ""),
            CompanyPhone = await settings.GetAsync("Company.Phone", "")
        };
        ViewData["Title"] = po.PoNumber;
        ViewData["Subtitle"] = $"Purchase order · {po.SupplierName} · {po.Status}";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> MarkOrdered(int id)
    {
        if (await purchaseService.MarkOrderedAsync(id)) TempData["Success"] = "Purchase order sent to supplier (status: Ordered).";
        else TempData["Error"] = "Only draft purchase orders can be marked as ordered.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Receive(int id, DateTime? receivedDate)
    {
        var date = (receivedDate ?? DateTime.Today).Date;
        if (date > DateTime.Today) { TempData["Error"] = "Received date cannot be in the future."; return RedirectToAction(nameof(Detail), new { id }); }
        var (ok, msg) = await purchaseService.ReceiveAsync(id, date, User.UserName());
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Cancel(int id)
    {
        if (await purchaseService.CancelAsync(id)) TempData["Success"] = "Purchase order cancelled.";
        else TempData["Error"] = "Received purchase orders cannot be cancelled.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> MarkPaid(int id)
    {
        if (await purchaseService.MarkPaidAsync(id)) TempData["Success"] = "Supplier payment recorded — linked finance entries marked Paid.";
        else TempData["Error"] = "This purchase order is already paid or was cancelled.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    private async Task FillLookupsAsync()
    {
        ViewBag.Suppliers = (await supplierService.GetAllAsync()).ToList();
        ViewBag.Farms = (await farmService.GetAllAsync()).ToList();
        ViewBag.Items = (await inventoryService.GetAllAsync()).ToList();
        ViewBag.NextPoNumber = await purchaseService.PreviewPoNumberAsync();
        ViewData["Title"] = "New Purchase Order";
        ViewData["Subtitle"] = "Raise a PO with one or more inventory line items";
    }
}
