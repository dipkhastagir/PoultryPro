using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

public partial class ReportsController
{
    [Authorize(Roles = Roles.Finance + "," + Roles.Viewer)]
    public async Task<IActionResult> Market(DateTime? from, DateTime? to)
    {
        var market = HttpContext.RequestServices.GetRequiredService<IMarketService>();
        var toDate = (to ?? DateTime.Today).Date;
        var fromDate = (from ?? new DateTime(toDate.Year, toDate.Month, 1).AddMonths(-11)).Date;
        if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);

        var all = (await market.GetAllAsync()).Where(o => o.Status != "Cancelled").ToList();
        var orders = all.Where(o => o.OrderDate.Date >= fromDate && o.OrderDate.Date <= toDate).ToList();

        var vm = new MarketReportVm
        {
            From = fromDate,
            To = toDate,
            OrderCount = orders.Count,
            TotalRevenue = orders.Sum(o => o.TotalAmount),
            TotalCollected = orders.Sum(o => o.PaidAmount),
            ByProduct = orders.GroupBy(o => o.Product).Select(g => new SalesBreakdownRow
            {
                Label = g.Key, Orders = g.Count(), Quantity = g.Sum(o => o.Quantity),
                Unit = g.Select(o => o.Unit).Distinct().Count() == 1 ? g.First().Unit : "mixed",
                Revenue = g.Sum(o => o.TotalAmount), Paid = g.Sum(o => o.PaidAmount),
                AvgPrice = g.Sum(o => o.Quantity) > 0 ? g.Sum(o => o.TotalAmount) / g.Sum(o => o.Quantity) : 0
            }).OrderByDescending(r => r.Revenue).ToList(),
            ByCustomer = orders.GroupBy(o => new { o.CustomerId, o.CustomerName }).Select(g => new SalesBreakdownRow
            {
                Label = g.Key.CustomerName, Orders = g.Count(), Quantity = g.Sum(o => o.Quantity),
                Revenue = g.Sum(o => o.TotalAmount), Paid = g.Sum(o => o.PaidAmount),
                AvgPrice = g.Count() > 0 ? g.Sum(o => o.TotalAmount) / g.Count() : 0
            }).OrderByDescending(r => r.Revenue).ToList(),
            // receivables are always as of today, regardless of the period filter
            Receivables = all.Where(o => o.BalanceDue > 0).OrderBy(o => o.DeliveryDate).ToList()
        };
        vm.TotalOutstanding = vm.Receivables.Sum(o => o.BalanceDue);
        for (var m = new DateTime(fromDate.Year, fromDate.Month, 1); m <= toDate; m = m.AddMonths(1))
        {
            var mm = m;
            var inMonth = orders.Where(o => o.OrderDate.Year == mm.Year && o.OrderDate.Month == mm.Month).ToList();
            vm.Monthly.Add((mm.ToString("MMM yy"), inMonth.Sum(o => o.TotalAmount), inMonth.Count));
        }
        ViewBag.CustomerIds = orders.GroupBy(o => o.CustomerName).ToDictionary(g => g.Key, g => g.First().CustomerId);
        ViewData["Title"] = "Sales Report";
        ViewData["Subtitle"] = $"{Fmt.Date(fromDate)} – {Fmt.Date(toDate)} · revenue by product and customer, trend and receivables";
        return View(vm);
    }
}
