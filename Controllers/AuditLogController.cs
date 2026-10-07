using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AuditLogController(IAuditService audit, IUserService users) : Controller
{
    public async Task<IActionResult> Index(string? user, string? entity, DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue && from > to) (from, to) = (to, from);
        var rows = (await audit.SearchAsync(user, entity, from, to, 1000)).ToList();
        var userNames = (await users.GetAllAsync()).Select(u => u.Username)
            .Concat(rows.Select(r => r.UserName).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();

        // activity per day across the visible range (or last 30 days when unfiltered)
        var end = (to ?? DateTime.Today).Date;
        var start = (from ?? (rows.Any() ? rows.Min(r => r.CreatedAt).Date : end.AddDays(-29))).Date;
        if ((end - start).TotalDays > 90) start = end.AddDays(-89);
        var counts = rows.GroupBy(r => r.CreatedAt.Date).ToDictionary(g => g.Key, g => g.Count());
        var perDay = new List<(DateTime, int)>();
        for (var d = start; d <= end; d = d.AddDays(1)) perDay.Add((d, counts.GetValueOrDefault(d)));

        var vm = new AuditLogVm
        {
            Rows = rows,
            Entities = (await audit.GetEntitiesAsync()).ToList(),
            Users = userNames,
            User = user, Entity = entity, From = from, To = to,
            PerDay = perDay,
            TopEntities = rows.GroupBy(r => r.Entity).Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).Take(6).ToList(),
            TopUsers = rows.GroupBy(r => r.UserName ?? "anonymous").Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).Take(6).ToList()
        };
        ViewData["Title"] = "Audit Log";
        ViewData["Subtitle"] = "Every create, update, delete and workflow action";
        return View(vm);
    }

    public async Task<IActionResult> Export(string? user, string? entity, DateTime? from, DateTime? to)
    {
        var rows = await audit.SearchAsync(user, entity, from, to, 100_000);
        var shaped = rows.Select(r => new
        {
            r.Id, Timestamp = r.CreatedAt, User = r.UserName, r.Action, r.Entity, r.EntityId, r.Details, r.IpAddress
        });
        return Csv.File(shaped, $"audit-log-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }
}
