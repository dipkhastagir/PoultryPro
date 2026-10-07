using Microsoft.AspNetCore.Mvc;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

/// <summary>Farm calendar aggregating vaccinations, tasks, harvests, deliveries and certificate expiries.</summary>
public class CalendarController(ICalendarService calendar) : Controller
{
    public async Task<IActionResult> Index(int? year, int? month)
    {
        var y = year is >= 2000 and <= 2100 ? year.Value : DateTime.Today.Year;
        var m = month is >= 1 and <= 12 ? month.Value : DateTime.Today.Month;
        var first = new DateTime(y, m, 1);
        // grid starts on Saturday (Bangladesh working week Sat–Thu, Friday weekend)
        var offset = ((int)first.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
        var gridStart = first.AddDays(-offset);
        var last = first.AddMonths(1).AddDays(-1);
        var endOffset = (6 - ((int)last.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7);
        var gridEnd = last.AddDays(endOffset);

        var today = DateTime.Today;
        var upcomingEnd = today.AddDays(14);
        var from = gridStart < today ? gridStart : today;
        var to = gridEnd > upcomingEnd ? gridEnd : upcomingEnd;
        var events = await calendar.GetEventsAsync(from, to);

        var vm = new CalendarMonthVm
        {
            Year = y, Month = m, GridStart = gridStart, GridEnd = gridEnd,
            Events = events.Where(e => e.Date >= gridStart && e.Date <= gridEnd).ToList(),
            Upcoming = events.Where(e => e.Date >= today && e.Date <= upcomingEnd).ToList(),
            OverdueVaccinations = await calendar.CountOverdueVaccinationsAsync()
        };
        ViewData["Title"] = "Farm Calendar";
        ViewData["Subtitle"] = $"Vaccinations, tasks, harvests, deliveries and certifications · {vm.MonthLabel}";
        return View(vm);
    }
}
