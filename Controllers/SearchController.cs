using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;

namespace PoultryPro.Controllers;

/// <summary>Global search (top-bar search box) across flocks, farms, people, commerce and the disease library.</summary>
public class SearchController(ISearchService search) : Controller
{
    public async Task<IActionResult> Index(string? q)
    {
        // Customers, suppliers and sales orders are only shown to roles that can open those modules
        var includeFinance = User.IsAny(Roles.Finance + "," + Roles.Viewer);
        var vm = await search.SearchAsync(q, includeFinance, perGroup: 10);
        ViewData["Title"] = "Search";
        ViewData["Subtitle"] = string.IsNullOrEmpty(vm.Query) ? "Search across the whole platform" : $"Results for “{vm.Query}”";
        return View(vm);
    }
}
