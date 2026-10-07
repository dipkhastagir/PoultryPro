using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

[Authorize(Roles = Roles.Admin)]
public class SettingsController(ISettingsService settings) : Controller
{
    public static readonly string[] GroupOrder = { "General", "Analytics", "Sustainability", "Integration", "Market" };

    public async Task<IActionResult> Index()
    {
        var all = (await settings.GetAllAsync()).ToList();
        var vm = new SettingsVm
        {
            Groups = all.GroupBy(s => s.GroupName)
                .OrderBy(g => Array.IndexOf(GroupOrder, g.Key) is var i && i >= 0 ? i : 99).ThenBy(g => g.Key).ToList(),
            Total = all.Count,
            LastUpdated = all.Count == 0 ? null : all.Max(s => s.UpdatedAt)
        };
        ViewData["Title"] = "Settings";
        ViewData["Subtitle"] = "Organisation details, alert thresholds, emission factors and integrations";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(List<string> keys, List<string?> values)
    {
        var current = (await settings.GetAllAsync()).ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);
        var changed = new List<string>();
        var errors = new List<string>();
        for (var i = 0; i < keys.Count && i < values.Count; i++)
        {
            var key = keys[i];
            var value = (values[i] ?? "").Trim();
            if (!current.TryGetValue(key, out var old)) continue;          // only existing keys can be edited
            if (string.Equals(old, value, StringComparison.Ordinal)) continue;
            if (value.Length > 500) { errors.Add($"{key}: value too long"); continue; }
            if (decimal.TryParse(old, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _)
                && !decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                errors.Add($"{key}: a number is required");
                continue;
            }
            await settings.SetAsync(key, value);
            changed.Add(key);
        }
        if (errors.Any()) TempData["Error"] = "Not saved — " + string.Join("; ", errors);
        if (changed.Any()) TempData["Success"] = $"{changed.Count} setting{(changed.Count == 1 ? "" : "s")} updated: {string.Join(", ", changed.Take(6))}{(changed.Count > 6 ? "…" : "")}";
        else if (!errors.Any()) TempData["Success"] = "No changes to save.";
        return RedirectToAction(nameof(Index));
    }
}
