using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

public class InventoryController(IInventoryService inventoryService, IFarmService farmService) : Controller
{
    public static readonly string[] Categories = { "Feed", "Medicine", "Vaccine", "Supplement", "Equipment", "Biosecurity" };

    public async Task<IActionResult> Index(string? category, string? status, string? q)
    {
        var all = (await inventoryService.GetAllAsync()).ToList();
        IEnumerable<InventoryItem> items = all;
        if (!string.IsNullOrEmpty(category))
            items = items.Where(i => i.Category == category);
        if (status == "low")
            items = items.Where(i => i.StockStatus != "In Stock");
        else if (status == "expiring")
            items = items.Where(i => i.IsExpiringSoon);
        if (!string.IsNullOrWhiteSpace(q))
            items = items.Where(i => i.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                                  || (i.SKU ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                                  || (i.SupplierName ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        var list = items.ToList();
        ViewBag.SelectedCategory = category;
        ViewBag.SelectedStatus = status;
        ViewBag.Q = q;
        ViewBag.Categories = all.Select(i => i.Category).Union(Categories).Distinct().OrderBy(c => c).ToList();
        ViewBag.TotalValue = list.Sum(i => i.TotalValue);
        ViewBag.LowCount = all.Count(i => i.StockStatus != "In Stock");
        ViewBag.OutCount = all.Count(i => i.StockStatus == "Out of Stock");
        ViewBag.ExpiringCount = all.Count(i => i.IsExpiringSoon);
        ViewBag.AllCount = all.Count;
        ViewBag.ValueByCategory = all.GroupBy(i => i.Category)
            .Select(g => (Category: g.Key, Value: g.Sum(i => i.TotalValue))).OrderByDescending(x => x.Value).ToList();
        ViewBag.Farms = (await farmService.GetAllAsync()).ToList();
        ViewData["Title"] = "Inventory Control";
        ViewData["Subtitle"] = "Feed, medicine, vaccines, equipment and supplies";
        return View(list);
    }

    public async Task<IActionResult> Ledger(int? itemId, string? type, DateTime? from, DateTime? to)
    {
        var vm = await BuildLedgerAsync(itemId, type, from, to);
        ViewData["Title"] = "Stock Movement Ledger";
        ViewData["Subtitle"] = vm.Item != null ? $"{vm.Item.Name} — every IN / OUT movement with running balance" : "Every IN / OUT movement across the store";
        return View(vm);
    }

    public async Task<IActionResult> ExportLedger(int? itemId, string? type, DateTime? from, DateTime? to)
    {
        var vm = await BuildLedgerAsync(itemId, type, from, to);
        var rows = vm.Entries.Select(e => new
        {
            Date = e.TxnDate, e.ItemName, e.SKU, e.Category, Type = e.TxnType, e.Quantity, e.Unit,
            UnitCost = e.UnitCost ?? 0, Value = e.Value, Balance = e.RunningBalance,
            Farm = e.FarmName, Batch = e.BatchCode, e.Reference, e.Reason, e.CreatedBy
        });
        return Csv.File(rows, $"stock-ledger-{DateTime.Today:yyyyMMdd}.csv");
    }

    private async Task<StockLedgerVm> BuildLedgerAsync(int? itemId, string? type, DateTime? from, DateTime? to)
    {
        var vm = new StockLedgerVm { ItemId = itemId, Type = string.IsNullOrEmpty(type) ? null : type, From = from, To = to };
        vm.Items = (await inventoryService.GetAllAsync()).ToList();
        vm.Item = itemId.HasValue ? vm.Items.FirstOrDefault(i => i.Id == itemId) : null;

        // Running balance per item: walk every movement oldest → newest, anchoring the final balance to the current on-hand quantity.
        var all = (await inventoryService.GetLedgerAsync(itemId)).OrderBy(e => e.TxnDate).ThenBy(e => e.Id).ToList();
        foreach (var g in all.GroupBy(e => e.ItemId))
        {
            var bal = g.First().CurrentQuantity - g.Sum(e => e.SignedQuantity);
            foreach (var e in g) { bal += e.SignedQuantity; e.RunningBalance = bal; }
        }

        IEnumerable<StockLedgerEntry> q = all;
        if (vm.Type != null) q = q.Where(e => e.TxnType == vm.Type);
        if (from.HasValue) q = q.Where(e => e.TxnDate.Date >= from.Value.Date);
        if (to.HasValue) q = q.Where(e => e.TxnDate.Date <= to.Value.Date);
        var filtered = q.ToList();

        if (vm.Item != null)
        {
            var before = all.Where(e => from.HasValue && e.TxnDate.Date < from.Value.Date).ToList();
            vm.OpeningBalance = all.Count == 0 ? vm.Item.Quantity
                : (all.First().RunningBalance - all.First().SignedQuantity) + before.Sum(e => e.SignedQuantity);
            var upTo = all.Where(e => !to.HasValue || e.TxnDate.Date <= to.Value.Date).ToList();
            vm.ClosingBalance = upTo.Count > 0 ? upTo.Last().RunningBalance : vm.OpeningBalance;
        }

        vm.TotalIn = filtered.Where(e => e.TxnType == "IN").Sum(e => e.Quantity);
        vm.TotalOut = filtered.Where(e => e.TxnType == "OUT").Sum(e => e.Quantity);
        vm.ValueIn = filtered.Where(e => e.TxnType == "IN").Sum(e => e.Value);
        vm.ValueOut = filtered.Where(e => e.TxnType == "OUT").Sum(e => e.Value);
        filtered.Reverse(); // newest first for display
        vm.Entries = filtered;
        return vm;
    }

    [Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Create()
    {
        await FillFormAsync("Add Inventory Item");
        return View(new InventoryItem { Category = "Feed", Unit = "kg" });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Create(InventoryItem item)
    {
        await ValidateItemAsync(item);
        if (!ModelState.IsValid)
        {
            await FillFormAsync("Add Inventory Item");
            return View(item);
        }
        await inventoryService.CreateAsync(item);
        TempData["Success"] = $"\"{item.Name}\" added to inventory.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await inventoryService.GetByIdAsync(id);
        if (item == null) return NotFound();
        await FillFormAsync("Edit Inventory Item");
        return View("Create", item);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Edit(InventoryItem item)
    {
        var existing = await inventoryService.GetByIdAsync(item.Id);
        if (existing == null) return NotFound();
        await ValidateItemAsync(item);
        if (!ModelState.IsValid)
        {
            await FillFormAsync("Edit Inventory Item");
            return View("Create", item);
        }
        // Quantity changes are posted to the ledger as a stock-count correction so the ledger always reconciles.
        var diff = item.Quantity - existing.Quantity;
        item.Quantity = existing.Quantity;
        await inventoryService.UpdateAsync(item);
        if (diff != 0)
            await inventoryService.StockAdjustAsync(item.Id, Math.Abs(diff), diff > 0 ? "IN" : "OUT", "Stock count correction",
                reference: "ADJ-" + DateTime.Now.ToString("yyMMddHHmm"), user: User.UserName());
        TempData["Success"] = diff != 0 ? $"Item updated; stock correction of {Fmt.Num(diff, 2)} {item.Unit} posted to the ledger." : "Item updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await inventoryService.DeleteAsync(id);
            TempData["Success"] = "Item removed.";
        }
        catch (Exception ex) when (ex.Message.Contains("referenced by purchase orders"))
        {
            TempData["Error"] = "This item appears on purchase orders and cannot be deleted. Set its minimum level to 0 instead.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> StockAdjust(int itemId, decimal qty, string type, string reason, int? farmId, string? reference, string? returnUrl)
    {
        var item = await inventoryService.GetByIdAsync(itemId);
        if (item == null) return NotFound();
        type = type == "OUT" ? "OUT" : "IN";
        if (qty <= 0) TempData["Error"] = "Quantity must be greater than zero.";
        else if (type == "OUT" && qty > item.Quantity) TempData["Error"] = $"Cannot issue {Fmt.Num(qty, 2)} {item.Unit} — only {Fmt.Num(item.Quantity, 2)} {item.Unit} on hand.";
        else
        {
            await inventoryService.StockAdjustAsync(itemId, qty, type, string.IsNullOrWhiteSpace(reason) ? (type == "IN" ? "Stock received" : "Issued to shed") : reason,
                farmId, string.IsNullOrWhiteSpace(reference) ? null : reference, User.UserName());
            TempData["Success"] = $"Stock {type} of {Fmt.Num(qty, 2)} {item.Unit} recorded for {item.Name}.";
        }
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    private async Task FillFormAsync(string title)
    {
        ViewBag.Suppliers = await inventoryService.GetSuppliersAsync();
        ViewBag.Categories = Categories;
        ViewData["Title"] = title;
        ViewData["Subtitle"] = "Item master data — SKU, units, re-order level, cost and supplier";
    }

    private async Task ValidateItemAsync(InventoryItem item)
    {
        ModelState.KeepPostedOnly(Request.Form);
        if (string.IsNullOrWhiteSpace(item.Name)) ModelState.AddModelError(nameof(item.Name), "Item name is required.");
        if (item.Quantity < 0) ModelState.AddModelError(nameof(item.Quantity), "Quantity cannot be negative.");
        if (item.MinimumLevel < 0) ModelState.AddModelError(nameof(item.MinimumLevel), "Minimum level cannot be negative.");
        if (item.UnitCost < 0) ModelState.AddModelError(nameof(item.UnitCost), "Unit cost cannot be negative.");
        item.SKU = string.IsNullOrWhiteSpace(item.SKU) ? null : item.SKU.Trim().ToUpperInvariant();
        if (item.SKU != null && await inventoryService.SkuExistsAsync(item.SKU, item.Id))
            ModelState.AddModelError(nameof(item.SKU), $"SKU {item.SKU} is already used by another item.");
    }
}
