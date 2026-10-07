using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

[Authorize(Roles = Roles.Finance)]
public class AttendanceController(IAttendanceService attendance) : Controller
{
    public async Task<IActionResult> Index(DateTime? date)
    {
        var d = (date ?? DateTime.Today).Date;
        if (d > DateTime.Today.AddDays(7)) d = DateTime.Today;
        var vm = await attendance.GetSheetAsync(d);
        ViewData["Title"] = "Daily Attendance";
        ViewData["Subtitle"] = d.ToString("dddd, dd MMMM yyyy");
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(DateTime date, List<AttendanceSaveRow> rows)
    {
        if (date == default) { TempData["Error"] = "Attendance date is missing."; return RedirectToAction(nameof(Index)); }
        if (date.Date > DateTime.Today.AddDays(7))
        {
            TempData["Error"] = "Attendance cannot be recorded that far in the future.";
            return RedirectToAction(nameof(Index));
        }
        var n = await attendance.SaveSheetAsync(date, rows ?? new());
        TempData["Success"] = $"Attendance saved for {date:dd MMM yyyy} ({n} employee{(n == 1 ? "" : "s")}).";
        return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> Report(int? year, int? month)
    {
        var (y, m) = Period(year, month);
        var vm = await attendance.GetMonthAsync(y, m);
        ViewData["Title"] = "Attendance Report";
        ViewData["Subtitle"] = $"Monthly attendance matrix · {vm.MonthLabel}";
        return View(vm);
    }

    public async Task<IActionResult> Export(int? year, int? month)
    {
        var (y, m) = Period(year, month);
        var rows = await attendance.GetExportRowsAsync(y, m);
        return Csv.File(rows, $"attendance-{y:0000}-{m:00}.csv");
    }

    private static (int, int) Period(int? year, int? month)
    {
        var y = year is >= 2000 and <= 2100 ? year.Value : DateTime.Today.Year;
        var m = month is >= 1 and <= 12 ? month.Value : DateTime.Today.Month;
        return (y, m);
    }
}
