using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

public class EmployeeController(IEmployeeService employeeService, IFarmService farmService) : Controller
{
    public static readonly string[] JobRoles =
    {
        "Farm Manager", "Shed Supervisor", "Shed Worker", "Veterinarian", "Lab Technician", "Research Assistant",
        "Egg Collector", "Egg Grader", "Store Keeper", "Inventory Manager", "Accountant", "Driver", "Security Guard"
    };
    public static readonly string[] Statuses = { "On Duty", "On Leave", "Off Duty", "Resigned" };

    public async Task<IActionResult> Index(string? q, int? farmId, string? role, string? status, bool inactive = false)
    {
        var all = (await employeeService.GetDirectoryAsync(inactive)).ToList();
        var active = all.Where(e => e.IsActive).ToList();
        IEnumerable<Employee> list = all;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            list = list.Where(e => e.FullName.Contains(t, StringComparison.OrdinalIgnoreCase)
                                   || (e.EmployeeCode ?? "").Contains(t, StringComparison.OrdinalIgnoreCase)
                                   || (e.Phone ?? "").Contains(t)
                                   || (e.Email ?? "").Contains(t, StringComparison.OrdinalIgnoreCase));
        }
        if (farmId.HasValue) list = farmId == 0 ? list.Where(e => e.FarmId == null) : list.Where(e => e.FarmId == farmId);
        if (!string.IsNullOrWhiteSpace(role)) list = list.Where(e => e.Role == role);
        if (!string.IsNullOrWhiteSpace(status)) list = list.Where(e => e.Status == status);

        var today = await employeeService.GetTodayAttendanceAsync();
        var vm = new EmployeeDirectoryVm
        {
            Employees = list.ToList(),
            Farms = (await farmService.GetAllAsync()).ToList(),
            Q = q, FarmId = farmId, Role = role, Status = status, ShowInactive = inactive,
            RoleCounts = active.GroupBy(e => e.Role).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
            TotalActive = active.Count,
            OnDuty = active.Count(e => e.Status == "On Duty"),
            OnLeave = active.Count(e => e.Status == "On Leave"),
            MonthlyPayroll = active.Sum(e => e.MonthlySalary),
            MarkedToday = today.Marked,
            AttendanceTodayPct = active.Count == 0 ? 0 : today.PresentWeight / active.Count * 100m
        };
        ViewData["Title"] = "Employees";
        ViewData["Subtitle"] = "Staff directory, roles, attendance and payroll overview";
        return View(vm);
    }

    public async Task<IActionResult> Detail(int id)
    {
        var vm = await employeeService.GetProfileAsync(id);
        if (vm == null) return NotFound();
        ViewData["Title"] = vm.Employee.FullName;
        ViewData["Subtitle"] = $"{vm.Employee.Role} · {vm.Employee.FarmName ?? "All farms"}";
        return View(vm);
    }

    [Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Create()
    {
        await LoadLookupsAsync();
        ViewData["Title"] = "Add Employee";
        ViewData["Subtitle"] = "Register a new staff member";
        return View(new Employee { JoinDate = DateTime.Today, Status = "On Duty", EmployeeCode = await employeeService.NextCodeAsync() });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Create(Employee employee)
    {
        if (!await ValidateAsync(employee))
        {
            await LoadLookupsAsync();
            ViewData["Title"] = "Add Employee";
            ViewData["Subtitle"] = "Register a new staff member";
            return View(employee);
        }
        var id = await employeeService.CreateAsync(employee);
        TempData["Success"] = $"{employee.FullName} added to the staff directory.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Edit(int id)
    {
        var emp = await employeeService.GetByIdAsync(id);
        if (emp == null) return NotFound();
        await LoadLookupsAsync();
        ViewData["Title"] = "Edit Employee";
        ViewData["Subtitle"] = emp.FullName;
        return View(emp);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Edit(Employee employee)
    {
        if (!await ValidateAsync(employee))
        {
            await LoadLookupsAsync();
            ViewData["Title"] = "Edit Employee";
            ViewData["Subtitle"] = employee.FullName;
            return View(employee);
        }
        await employeeService.UpdateAsync(employee);
        TempData["Success"] = "Employee updated.";
        return RedirectToAction(nameof(Detail), new { id = employee.Id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> Delete(int id)
    {
        await employeeService.DeleteAsync(id);
        TempData["Success"] = "Employee deactivated (history is kept).";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> ValidateAsync(Employee e)
    {
        ModelState.Remove(nameof(Employee.FarmName));
        if (string.IsNullOrWhiteSpace(e.FullName)) ModelState.AddModelError(nameof(e.FullName), "Full name is required.");
        if (string.IsNullOrWhiteSpace(e.Role)) ModelState.AddModelError(nameof(e.Role), "Role is required.");
        if (e.MonthlySalary < 0) ModelState.AddModelError(nameof(e.MonthlySalary), "Salary cannot be negative.");
        if (e.JoinDate == default) ModelState.AddModelError(nameof(e.JoinDate), "Join date is required.");
        else if (e.JoinDate.Date > DateTime.Today.AddDays(60)) ModelState.AddModelError(nameof(e.JoinDate), "Join date is too far in the future.");
        e.EmployeeCode = string.IsNullOrWhiteSpace(e.EmployeeCode) ? null : e.EmployeeCode.Trim().ToUpperInvariant();
        if (e.EmployeeCode != null && await employeeService.CodeExistsAsync(e.EmployeeCode, e.Id))
            ModelState.AddModelError(nameof(e.EmployeeCode), "This employee code is already used.");
        if (e.Id == 0) e.IsActive = true;
        return ModelState.IsValid;
    }

    private async Task LoadLookupsAsync()
    {
        ViewBag.Farms = (await farmService.GetAllAsync()).ToList();
        ViewBag.JobRoles = JobRoles;
        ViewBag.Statuses = Statuses;
    }
}
