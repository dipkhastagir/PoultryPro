using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;

namespace PoultryPro.Controllers;

/// <summary>Research data hub: CSV exports, data dictionary and correlation analysis (read-only).</summary>
public class ResearchController(IResearchService research) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Research Data";
        ViewData["Subtitle"] = "Tidy, analysis-ready exports of every data set collected by the platform";
        return View(await research.GetIndexAsync());
    }

    public async Task<IActionResult> Export(string dataset, int? farmId, DateTime? from, DateTime? to)
    {
        var result = await research.ExportAsync(dataset, farmId, from, to);
        if (result == null)
        {
            TempData["Error"] = $"Unknown dataset '{dataset}'.";
            return RedirectToAction(nameof(Index));
        }
        var rows = result.Value.Rows.ToList();
        if (rows.Count == 0)
        {
            TempData["Error"] = "No rows match the selected filters.";
            return RedirectToAction(nameof(Index));
        }
        return Csv.File(rows, result.Value.FileName);
    }

    public async Task<IActionResult> DataDictionary()
    {
        ViewData["Title"] = "Data Dictionary";
        ViewData["Subtitle"] = "Every table and view with column types and nullability (live from INFORMATION_SCHEMA)";
        return View(await research.GetDataDictionaryAsync());
    }

    public async Task<IActionResult> Correlations(string? batchType, int? farmId)
    {
        ViewData["Title"] = "Correlation Explorer";
        ViewData["Subtitle"] = "Pearson correlation matrix of batch-day variables";
        return View(await research.GetCorrelationsAsync(batchType, farmId));
    }
}
