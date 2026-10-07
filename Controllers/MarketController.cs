using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

[Authorize(Roles = Roles.Finance + "," + Roles.Viewer)]
public class MarketController(IMarketService marketService, IFarmService farmService, ISettingsService settings) : Controller
{
    public static readonly string[] OrderStatuses = { "Pending", "Confirmed", "Processing", "Dispatched", "Out for Delivery", "Delivered", "Cancelled" };
    public static readonly string[] Products = { "Live Broiler", "Live Sonali", "Live Layer", "Brown Eggs", "White Eggs", "Table Eggs", "Dressed Chicken", "Spent Hen", "Day-old Chicks", "Poultry Manure (organic)", "Offal" };
    public static readonly string[] CustomerTypes = { "Retailer", "Wholesaler", "Distributor", "Super Shop", "Restaurant", "E-commerce", "Processor", "Institution" };
    public static readonly string[] PaymentMethods = { "Cash", "bKash", "Nagad", "Bank Transfer", "Cheque" };

    public async Task<IActionResult> Index(string? status, string? payment, string? q)
    {
        var orders = (await marketService.GetAllAsync()).ToList();
        ViewBag.ActiveOrders = orders.Count(o => o.IsOpen);
        ViewBag.TotalRevenue = orders.Where(o => o.Status == "Delivered").Sum(o => o.TotalAmount);
        ViewBag.CustomerCount = orders.Select(o => o.CustomerId).Distinct().Count();
        ViewBag.Receivable = orders.Where(o => o.Status != "Cancelled").Sum(o => o.BalanceDue);
        ViewBag.LateCount = orders.Count(o => o.IsLate);
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        ViewBag.MonthSales = orders.Where(o => o.Status != "Cancelled" && o.OrderDate >= monthStart).Sum(o => o.TotalAmount);
        ViewBag.StatusCounts = OrderStatuses.ToDictionary(s => s, s => orders.Count(o => o.Status == s));
        ViewBag.Status = status;
        ViewBag.Payment = payment;
        ViewBag.Q = q;

        IEnumerable<Order> list = orders;
        if (status == "open") list = list.Where(o => o.IsOpen);
        else if (!string.IsNullOrEmpty(status)) list = list.Where(o => o.Status == status);
        if (!string.IsNullOrEmpty(payment)) list = list.Where(o => o.PaymentStatus == payment && o.Status != "Cancelled");
        if (!string.IsNullOrWhiteSpace(q))
            list = list.Where(o => o.OrderCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                                || o.CustomerName.Contains(q, StringComparison.OrdinalIgnoreCase)
                                || o.Product.Contains(q, StringComparison.OrdinalIgnoreCase)
                                || (o.BatchCode ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        ViewData["Title"] = "Sales Orders";
        ViewData["Subtitle"] = "Orders, deliveries, invoices and collections";
        return View(list.ToList());
    }

    public async Task<IActionResult> Detail(int id)
    {
        var vm = await BuildOrderVmAsync(id);
        if (vm == null) return NotFound();
        ViewData["Title"] = vm.Order.OrderCode;
        ViewData["Subtitle"] = $"Sales order · {vm.Order.CustomerName} · {vm.Order.Status}";
        return View(vm);
    }

    public async Task<IActionResult> Invoice(int id)
    {
        var vm = await BuildOrderVmAsync(id);
        if (vm == null) return NotFound();
        ViewData["Title"] = "Invoice " + vm.Order.OrderCode;
        ViewData["Subtitle"] = vm.Order.CustomerName;
        return View(vm);
    }

    private async Task<OrderDetailVm?> BuildOrderVmAsync(int id)
    {
        var o = await marketService.GetByIdAsync(id);
        if (o == null) return null;
        return new OrderDetailVm
        {
            Order = o,
            Customer = await marketService.GetCustomerAccountAsync(o.CustomerId),
            Batch = o.BatchId.HasValue ? await marketService.GetBatchAsync(o.BatchId.Value) : null,
            Payments = (await marketService.GetPaymentsAsync(o.OrderCode)).ToList(),
            CompanyName = await settings.GetAsync("Company.Name", "PoultryPro Integrated Farms Ltd."),
            CompanyAddress = await settings.GetAsync("Company.Address", ""),
            CompanyPhone = await settings.GetAsync("Company.Phone", "")
        };
    }

    [Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Create(int? customerId, int? batchId)
    {
        var order = new Order { OrderDate = DateTime.Today, DeliveryDate = DateTime.Today.AddDays(2), Status = "Pending", PaymentStatus = "Unpaid", CustomerId = customerId ?? 0 };
        if (batchId.HasValue)
        {
            var b = await marketService.GetBatchAsync(batchId.Value);
            if (b != null) { order.BatchId = b.Id; order.FarmId = b.FarmId; order.BirdCount = b.CurrentCount; order.Product = b.BatchType == "Sonali" ? "Live Sonali" : "Live Broiler"; }
        }
        await FillOrderFormAsync("New Sales Order");
        return View(order);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Create(Order order, decimal advance = 0, string? paymentMethod = null)
    {
        await ValidateOrderAsync(order);
        if (advance < 0) ModelState.AddModelError("advance", "Advance cannot be negative.");
        if (advance > order.Quantity * order.UnitPrice) ModelState.AddModelError("advance", "Advance cannot exceed the order total.");
        if (!ModelState.IsValid)
        {
            await FillOrderFormAsync("New Sales Order");
            ViewBag.Advance = advance;
            return View(order);
        }
        var id = await marketService.CreateAsync(order, advance, paymentMethod, User.UserName());
        TempData["Success"] = $"Order {order.OrderCode} created" + (advance > 0 ? $" with advance {Fmt.Money(advance)}." : ".");
        return RedirectToAction(nameof(Detail), new { id });
    }

    [Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Edit(int id)
    {
        var o = await marketService.GetByIdAsync(id);
        if (o == null) return NotFound();
        await FillOrderFormAsync("Edit Order " + o.OrderCode);
        return View(o);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Edit(Order order)
    {
        var existing = await marketService.GetByIdAsync(order.Id);
        if (existing == null) return NotFound();
        await ValidateOrderAsync(order);
        if (order.Quantity * order.UnitPrice < existing.PaidAmount)
            ModelState.AddModelError(nameof(order.UnitPrice), $"New total would be below the amount already received ({Fmt.Money(existing.PaidAmount)}).");
        if (!ModelState.IsValid)
        {
            order.OrderCode = existing.OrderCode;
            order.PaidAmount = existing.PaidAmount;
            await FillOrderFormAsync("Edit Order " + existing.OrderCode);
            return View(order);
        }
        await marketService.UpdateAsync(order);
        TempData["Success"] = $"Order {existing.OrderCode} updated.";
        return RedirectToAction(nameof(Detail), new { id = order.Id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> UpdateStatus(int id, string status, string? paymentStatus)
    {
        var o = await marketService.GetByIdAsync(id);
        if (o == null) return NotFound();
        if (!OrderStatuses.Contains(status)) { TempData["Error"] = "Unknown order status."; return RedirectToAction(nameof(Index)); }
        if (string.IsNullOrEmpty(paymentStatus)) await marketService.SetStatusAsync(id, status);
        else await marketService.UpdateStatusAsync(id, status, paymentStatus);
        TempData["Success"] = $"{o.OrderCode} → {status}.";
        var referer = Request.Headers.Referer.ToString();
        return referer.Contains("/Market/Detail", StringComparison.OrdinalIgnoreCase) ? RedirectToAction(nameof(Detail), new { id }) : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> RecordPayment(int orderId, decimal amount, DateTime? paymentDate, string? paymentMethod, string? note, string? returnUrl)
    {
        var date = (paymentDate ?? DateTime.Today).Date;
        if (date > DateTime.Today) TempData["Error"] = "Payment date cannot be in the future.";
        else
        {
            var (ok, msg) = await marketService.RecordPaymentAsync(orderId, amount, date, paymentMethod, note, User.UserName());
            TempData[ok ? "Success" : "Error"] = msg;
        }
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction(nameof(Detail), new { id = orderId });
    }

    /// <summary>JSON list of active batches for a farm (used by the order form).</summary>
    public async Task<IActionResult> Batches(int farmId)
    {
        var list = await marketService.GetActiveBatchesAsync(farmId);
        return Json(list.Select(b => new { b.Id, b.BatchCode, b.Breed, b.BatchType, b.CurrentCount, b.AgeInDays, b.AverageWeightKg }));
    }

    // ───────────── Customers ─────────────
    public async Task<IActionResult> Customers(string? type, string? q, bool inactive = false)
    {
        var all = (await marketService.GetCustomerAccountsAsync(includeInactive: true)).ToList();
        IEnumerable<CustomerAccount> list = all.Where(c => inactive || c.IsActive);
        if (!string.IsNullOrEmpty(type)) list = list.Where(c => c.CustomerType == type);
        if (!string.IsNullOrWhiteSpace(q))
            list = list.Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || (c.Phone ?? "").Contains(q) || (c.Address ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        var active = all.Where(c => c.IsActive).ToList();
        ViewBag.Types = all.Select(c => c.CustomerType).Union(CustomerTypes).Distinct().OrderBy(t => t).ToList();
        ViewBag.Type = type;
        ViewBag.Q = q;
        ViewBag.Inactive = inactive;
        ViewBag.ActiveCount = active.Count;
        ViewBag.TotalBilled = active.Sum(c => c.TotalBilled);
        ViewBag.Outstanding = active.Sum(c => c.Outstanding);
        ViewBag.OverLimit = active.Count(c => c.OverLimit);
        ViewData["Title"] = "Customers";
        ViewData["Subtitle"] = "Customer accounts, sales totals and receivables";
        return View(list.OrderByDescending(c => c.Outstanding).ThenByDescending(c => c.TotalBilled).ToList());
    }

    public async Task<IActionResult> CustomerDetail(int id)
    {
        var c = await marketService.GetCustomerAccountAsync(id);
        if (c == null) return NotFound();
        var vm = new CustomerDetailVm
        {
            Customer = c,
            Orders = (await marketService.GetByCustomerAsync(id)).ToList(),
            Payments = (await marketService.GetCustomerPaymentsAsync(id)).ToList()
        };
        var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
        for (var m = start; m <= DateTime.Today; m = m.AddMonths(1))
        {
            var mm = m;
            vm.MonthlySales.Add((mm.ToString("MMM yy"), vm.Orders.Where(o => o.Status != "Cancelled" && o.OrderDate.Year == mm.Year && o.OrderDate.Month == mm.Month).Sum(o => o.TotalAmount)));
        }
        ViewData["Title"] = c.Name;
        ViewData["Subtitle"] = $"Customer statement · {c.CustomerType}";
        return View(vm);
    }

    [Authorize(Roles = Roles.Finance)]
    public IActionResult AddCustomer()
    {
        ViewBag.Types = CustomerTypes;
        ViewData["Title"] = "Add Customer";
        ViewData["Subtitle"] = "Register a buyer account";
        return View(new Customer());
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> AddCustomer(Customer customer)
    {
        ValidateCustomer(customer);
        if (!ModelState.IsValid)
        {
            ViewBag.Types = CustomerTypes;
            ViewData["Title"] = "Add Customer";
            ViewData["Subtitle"] = "Register a buyer account";
            return View(customer);
        }
        var id = await marketService.CreateCustomerAsync(customer);
        TempData["Success"] = $"Customer \"{customer.Name}\" added.";
        return RedirectToAction(nameof(CustomerDetail), new { id });
    }

    [Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> EditCustomer(int id)
    {
        var c = await marketService.GetCustomerByIdAsync(id);
        if (c == null) return NotFound();
        ViewBag.Types = CustomerTypes.Union(new[] { c.CustomerType }).Distinct().ToArray();
        ViewData["Title"] = "Edit Customer";
        ViewData["Subtitle"] = c.Name;
        return View(c);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> EditCustomer(Customer customer)
    {
        ValidateCustomer(customer);
        if (!ModelState.IsValid)
        {
            ViewBag.Types = CustomerTypes.Union(new[] { customer.CustomerType }).Distinct().ToArray();
            ViewData["Title"] = "Edit Customer";
            ViewData["Subtitle"] = customer.Name;
            return View(customer);
        }
        await marketService.UpdateCustomerAsync(customer);
        TempData["Success"] = $"Customer \"{customer.Name}\" updated.";
        return RedirectToAction(nameof(CustomerDetail), new { id = customer.Id });
    }

    private void ValidateCustomer(Customer c)
    {
        ModelState.KeepPostedOnly(Request.Form);
        if (string.IsNullOrWhiteSpace(c.Name)) ModelState.AddModelError(nameof(c.Name), "Customer name is required.");
        if (c.CreditLimit < 0) ModelState.AddModelError(nameof(c.CreditLimit), "Credit limit cannot be negative.");
        if (!string.IsNullOrWhiteSpace(c.Email) && !c.Email.Contains('@')) ModelState.AddModelError(nameof(c.Email), "Enter a valid email address.");
        if (string.IsNullOrWhiteSpace(c.CustomerType)) c.CustomerType = "Retailer";
    }

    private async Task ValidateOrderAsync(Order o)
    {
        ModelState.KeepPostedOnly(Request.Form);
        if (o.CustomerId <= 0) ModelState.AddModelError(nameof(o.CustomerId), "Choose a customer.");
        if (o.FarmId <= 0) ModelState.AddModelError(nameof(o.FarmId), "Choose the source farm.");
        if (string.IsNullOrWhiteSpace(o.Product)) ModelState.AddModelError(nameof(o.Product), "Product is required.");
        if (o.Quantity <= 0) ModelState.AddModelError(nameof(o.Quantity), "Quantity must be greater than zero.");
        if (o.UnitPrice < 0) ModelState.AddModelError(nameof(o.UnitPrice), "Unit price cannot be negative.");
        if (o.DeliveryDate.Date < o.OrderDate.Date) ModelState.AddModelError(nameof(o.DeliveryDate), "Delivery date cannot be before the order date.");
        if (o.BirdCount is < 0) ModelState.AddModelError(nameof(o.BirdCount), "Bird count cannot be negative.");
        if (!OrderStatuses.Contains(o.Status)) o.Status = "Pending";
        if (o.BatchId.HasValue)
        {
            var b = await marketService.GetBatchAsync(o.BatchId.Value);
            if (b == null) ModelState.AddModelError(nameof(o.BatchId), "Selected batch not found.");
            else
            {
                if (b.FarmId != o.FarmId) ModelState.AddModelError(nameof(o.BatchId), "The batch does not belong to the selected farm.");
                if (o.BirdCount > b.CurrentCount && o.Id == 0) ModelState.AddModelError(nameof(o.BirdCount), $"Batch {b.BatchCode} has only {b.CurrentCount:N0} birds.");
            }
        }
    }

    private async Task FillOrderFormAsync(string title)
    {
        ViewBag.Customers = (await marketService.GetCustomerAccountsAsync()).ToList();
        ViewBag.Farms = (await farmService.GetAllAsync()).ToList();
        ViewBag.Batches = (await marketService.GetActiveBatchesAsync()).ToList();
        ViewBag.Products = Products;
        ViewBag.Statuses = OrderStatuses;
        ViewBag.PaymentMethods = PaymentMethods;
        ViewBag.PriceBroiler = await settings.GetDecimalAsync("Price.BroilerPerKg", 165);
        ViewBag.PriceSonali = await settings.GetDecimalAsync("Price.SonaliPerKg", 260);
        ViewBag.PriceEgg = await settings.GetDecimalAsync("Price.EggPerPiece", 11.5m);
        ViewData["Title"] = title;
        ViewData["Subtitle"] = "Customer, source flock, quantity and pricing";
    }
}
