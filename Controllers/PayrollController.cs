using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

[Authorize(Roles = Roles.Finance)]
public class PayrollController(IPayrollService payroll, ISettingsService settings) : Controller
{
    public async Task<IActionResult> Index(int? year, int? month)
    {
        var (y, m) = Period(year, month);
        var vm = await payroll.GetRegisterAsync(y, m);
        ViewData["Title"] = "Payroll";
        ViewData["Subtitle"] = $"Payroll register · {vm.MonthLabel}";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(int year, int month)
    {
        var (y, m) = Period(year, month);
        if (new DateTime(y, m, 1) > DateTime.Today)
        {
            TempData["Error"] = "Payroll cannot be generated for a future month.";
            return RedirectToAction(nameof(Index), new { year = y, month = m });
        }
        var n = await payroll.GenerateAsync(y, m);
        TempData[n > 0 ? "Success" : "Error"] = n > 0
            ? $"Generated payroll for {n} employee{(n == 1 ? "" : "s")} ({new DateTime(y, m, 1):MMMM yyyy})."
            : "All active employees already have payroll for this month.";
        return RedirectToAction(nameof(Index), new { year = y, month = m });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var p = await payroll.GetByIdAsync(id);
        if (p == null) return NotFound();
        if (p.Status == "Paid")
        {
            TempData["Error"] = "This payroll entry has already been paid and is locked.";
            return RedirectToAction(nameof(Payslip), new { id });
        }
        ViewData["Title"] = "Adjust Payroll";
        ViewData["Subtitle"] = $"{p.FullName} · {p.PeriodLabel}";
        return View(new PayrollEditVm
        {
            Entry = p, Allowances = p.Allowances, Overtime = p.Overtime, Deductions = p.Deductions,
            DaysPresent = p.DaysPresent, DaysAbsent = p.DaysAbsent
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PayrollEditVm vm)
    {
        var p = await payroll.GetByIdAsync(id);
        if (p == null) return NotFound();
        if (p.Status == "Paid")
        {
            TempData["Error"] = "Paid payroll entries cannot be changed.";
            return RedirectToAction(nameof(Index), new { year = p.PeriodYear, month = p.PeriodMonth });
        }
        ModelState.Remove("Entry.FullName");
        foreach (var k in ModelState.Keys.Where(k => k.StartsWith("Entry")).ToList()) ModelState.Remove(k);
        if (vm.Deductions > p.BasicSalary + vm.Allowances + vm.Overtime)
            ModelState.AddModelError(nameof(vm.Deductions), "Deductions cannot exceed gross pay.");
        if (vm.DaysPresent + vm.DaysAbsent > 31)
            ModelState.AddModelError(nameof(vm.DaysAbsent), "Present + absent days cannot exceed 31.");
        if (!ModelState.IsValid)
        {
            vm.Entry = p;
            ViewData["Title"] = "Adjust Payroll";
            ViewData["Subtitle"] = $"{p.FullName} · {p.PeriodLabel}";
            return View(vm);
        }
        await payroll.UpdateAsync(id, vm.Allowances, vm.Overtime, vm.Deductions, vm.DaysPresent, vm.DaysAbsent);
        TempData["Success"] = $"Payroll updated for {p.FullName}.";
        return RedirectToAction(nameof(Index), new { year = p.PeriodYear, month = p.PeriodMonth });
    }

    /// <summary>Pays a single entry (id) or every pending entry of a month (year + month), posting Salary transactions.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(int? id, int? year, int? month)
    {
        int n;
        int y, m;
        if (id.HasValue)
        {
            var p = await payroll.GetByIdAsync(id.Value);
            if (p == null) return NotFound();
            (y, m) = (p.PeriodYear, p.PeriodMonth);
            n = await payroll.MarkPaidAsync(new[] { id.Value });
            TempData[n > 0 ? "Success" : "Error"] = n > 0 ? $"{p.FullName} marked as paid — {Fmt.Money(p.NetPay)} posted to finance." : "Entry was already paid.";
        }
        else
        {
            (y, m) = Period(year, month);
            n = await payroll.MarkMonthPaidAsync(y, m);
            TempData[n > 0 ? "Success" : "Error"] = n > 0 ? $"{n} salaries marked as paid and posted to finance." : "There are no pending entries for this month.";
        }
        return RedirectToAction(nameof(Index), new { year = y, month = m });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await payroll.GetByIdAsync(id);
        if (p == null) return NotFound();
        if (p.Status == "Paid") TempData["Error"] = "Paid entries cannot be deleted.";
        else
        {
            await payroll.DeleteAsync(id);
            TempData["Success"] = $"Payroll entry for {p.FullName} removed. Re-run Generate to rebuild it from attendance.";
        }
        return RedirectToAction(nameof(Index), new { year = p.PeriodYear, month = p.PeriodMonth });
    }

    public async Task<IActionResult> Payslip(int id)
    {
        var p = await payroll.GetByIdAsync(id);
        if (p == null) return NotFound();
        ViewData["Title"] = "Payslip";
        ViewData["Subtitle"] = $"{p.FullName} · {p.PeriodLabel}";
        return View(new PayslipVm
        {
            Entry = p,
            CompanyName = await settings.GetAsync("Company.Name", "PoultryPro"),
            CompanyAddress = await settings.GetAsync("Company.Address", ""),
            CompanyPhone = await settings.GetAsync("Company.Phone", ""),
            WorkingDays = PayrollService.WorkingDaysPerMonth,
            DailyRate = p.BasicSalary / PayrollService.WorkingDaysPerMonth,
            AmountInWords = PayrollService.AmountInWords(p.NetPay)
        });
    }

    private static (int, int) Period(int? year, int? month)
    {
        var y = year is >= 2000 and <= 2100 ? year.Value : DateTime.Today.Year;
        var m = month is >= 1 and <= 12 ? month.Value : DateTime.Today.Month;
        return (y, m);
    }
}
