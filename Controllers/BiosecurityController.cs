using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

public class BiosecurityController(IBiosecurityService bioService, IFarmService farmService) : Controller
{
    public async Task<IActionResult> Index(int? farmId)
    {
        var farms = (await farmService.GetAllAsync()).OrderBy(f => f.Name).ToList();
        var all = await bioService.GetAuditsAsync();
        var vm = new BiosecurityIndexVm
        {
            Farms = farms,
            FarmId = farmId,
            Audits = farmId.HasValue ? all.Where(a => a.FarmId == farmId.Value).ToList() : all,
            LatestPerFarm = all.GroupBy(a => a.FarmId)
                               .Select(g => g.OrderByDescending(a => a.AuditDate).ThenByDescending(a => a.Id).First())
                               .Where(a => !farmId.HasValue || a.FarmId == farmId.Value)
                               .OrderBy(a => a.FarmName).ToList()
        };
        var farmName = farmId.HasValue ? farms.FirstOrDefault(f => f.Id == farmId)?.Name : null;
        ViewData["Title"] = "Biosecurity Audits";
        ViewData["Subtitle"] = farmName != null
            ? $"Weighted 14-point biosecurity checklist history — {farmName}"
            : "Weighted 14-point biosecurity checklist — scores, grades and trends across farms";
        return View(vm);
    }

    [Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Create(int? farmId)
    {
        var vm = new BiosecurityChecklistVm
        {
            FarmId = farmId ?? 0,
            AuditDate = DateTime.Today,
            AuditorName = User.Identity?.Name != null ? User.FullName() : "",
            Items = bioService.Checklist.Select(q => new BiosecurityAnswerVm
            {
                Category = q.Category, Question = q.Question, Weight = q.Weight
            }).ToList()
        };
        await LoadFormAsync();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Health)]
    public async Task<IActionResult> Create(BiosecurityChecklistVm vm)
    {
        // Re-attach the server-side question text/weights (only Passed/Remark come from the form).
        var checklist = bioService.Checklist;
        for (var i = 0; i < checklist.Count; i++)
        {
            if (i >= vm.Items.Count) vm.Items.Add(new BiosecurityAnswerVm());
            vm.Items[i].Category = checklist[i].Category;
            vm.Items[i].Question = checklist[i].Question;
            vm.Items[i].Weight = checklist[i].Weight;
        }
        if (vm.Items.Count > checklist.Count) vm.Items = vm.Items.Take(checklist.Count).ToList();
        ModelState.KeepPostedOnly(Request.Form); // question text/category are server-side, not posted

        if (vm.AuditDate.Date > DateTime.Today)
            ModelState.AddModelError(nameof(vm.AuditDate), "The audit date cannot be in the future.");
        if (vm.FarmId > 0 && await farmService.GetByIdAsync(vm.FarmId) == null)
            ModelState.AddModelError(nameof(vm.FarmId), "Select a valid farm.");

        if (!ModelState.IsValid)
        {
            await LoadFormAsync();
            return View(vm);
        }

        var id = await bioService.CreateAuditAsync(vm);
        var audit = await bioService.GetAuditAsync(id);
        TempData["Success"] = audit != null
            ? $"Audit saved — {audit.FarmName}: {audit.ScorePct:0.#}% (Grade {audit.Grade})."
            : "Biosecurity audit saved.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    public async Task<IActionResult> Detail(int id)
    {
        var audit = await bioService.GetAuditAsync(id);
        if (audit == null) return NotFound();
        var vm = new BiosecurityDetailVm
        {
            Audit = audit,
            Items = await bioService.GetItemsAsync(id),
            Previous = await bioService.GetPreviousAuditAsync(audit.FarmId, audit.AuditDate, audit.Id)
        };
        if (vm.Previous != null) vm.PreviousItems = await bioService.GetItemsAsync(vm.Previous.Id);

        var categoryOrder = bioService.Checklist.Select(q => q.Category).Distinct().ToList();
        foreach (var cat in vm.Items.Select(i => i.Category).Distinct()
                     .OrderBy(c => categoryOrder.IndexOf(c) < 0 ? 99 : categoryOrder.IndexOf(c)))
        {
            var items = vm.Items.Where(i => i.Category == cat).ToList();
            var row = new BiosecurityCategoryScore
            {
                Category = cat,
                Score = items.Where(i => i.Passed).Sum(i => i.Weight),
                Max = items.Sum(i => i.Weight)
            };
            var prev = vm.PreviousItems.Where(i => i.Category == cat).ToList();
            if (prev.Count > 0)
            {
                var pmax = prev.Sum(i => i.Weight);
                row.PreviousPct = pmax > 0 ? Math.Round((decimal)prev.Where(i => i.Passed).Sum(i => i.Weight) / pmax * 100, 1) : 0;
            }
            vm.Categories.Add(row);
        }

        ViewData["Title"] = $"Biosecurity Audit — {audit.FarmName}";
        ViewData["Subtitle"] = $"{audit.AuditDate:dd MMM yyyy} · audited by {audit.AuditorName}";
        return View(vm);
    }

    private async Task LoadFormAsync()
    {
        ViewBag.Farms = (await farmService.GetAllAsync()).Where(f => f.IsActive).OrderBy(f => f.Name).ToList();
        ViewData["Title"] = "New Biosecurity Audit";
        ViewData["Subtitle"] = "Score each control point — weights and grade are calculated automatically (A ≥ 90%, B ≥ 75%, C ≥ 60%, else D)";
    }
}
