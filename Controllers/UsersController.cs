using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

[Authorize(Roles = Roles.Admin)]
public class UsersController(IUserService users, IFieldOfficerService fieldOfficers) : Controller
{
    /// <summary>What each role can do — rendered as the permissions matrix on the Users page.</summary>
    public static readonly List<(string Area, string[] Roles)> PermissionMatrix = new()
    {
        ("Dashboards, analytics & research (read)", Roles.All),
        ("Farms, batches & daily records", new[] { Roles.Admin, Roles.Manager, Roles.Veterinarian, Roles.FieldOfficer }),
        ("Flock health, vaccination & biosecurity", new[] { Roles.Admin, Roles.Manager, Roles.Veterinarian }),
        ("Field visits & mobile sync", new[] { Roles.Admin, Roles.Manager, Roles.FieldOfficer }),
        ("Inventory, suppliers & purchasing", new[] { Roles.Admin, Roles.Manager, Roles.Accountant }),
        ("Sales orders & customers", new[] { Roles.Admin, Roles.Manager, Roles.Accountant }),
        ("Finance, budgets & transactions", new[] { Roles.Admin, Roles.Manager, Roles.Accountant }),
        ("Employees, attendance & payroll", new[] { Roles.Admin, Roles.Manager, Roles.Accountant }),
        ("Tasks & calendar (create / edit)", new[] { Roles.Admin, Roles.Manager, Roles.Veterinarian, Roles.Accountant, Roles.FieldOfficer }),
        ("Users, settings & audit log", new[] { Roles.Admin })
    };

    public async Task<IActionResult> Index()
    {
        var vm = new UsersIndexVm
        {
            Users = (await users.GetAllAsync()).ToList(),
            CurrentUserId = User.UserId(),
            Matrix = PermissionMatrix
        };
        ViewData["Title"] = "Users & Roles";
        ViewData["Subtitle"] = "Login accounts, roles and access rights";
        return View(vm);
    }

    public async Task<IActionResult> Create()
    {
        await LoadLookupsAsync();
        ViewData["Title"] = "New User";
        ViewData["Subtitle"] = "Create a login account";
        return View("Form", new UserFormVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormVm vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Password)) ModelState.AddModelError(nameof(vm.Password), "A password is required for new users.");
        await ValidateAsync(vm);
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            ViewData["Title"] = "New User";
            ViewData["Subtitle"] = "Create a login account";
            return View("Form", vm);
        }
        await users.CreateAsync(ToUser(vm), vm.Password!);
        TempData["Success"] = $"User '{vm.Username}' created with role {vm.Role}.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var u = await users.GetByIdAsync(id);
        if (u == null) return NotFound();
        await LoadLookupsAsync();
        ViewData["Title"] = "Edit User";
        ViewData["Subtitle"] = u.FullName;
        return View("Form", new UserFormVm
        {
            Id = u.Id, FullName = u.FullName, Username = u.Username, Email = u.Email, Phone = u.Phone, Role = u.Role,
            FieldOfficerId = u.FieldOfficerId, IsActive = u.IsActive, LastLoginAt = u.LastLoginAt, CreatedAt = u.CreatedAt
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserFormVm vm)
    {
        var existing = await users.GetByIdAsync(vm.Id);
        if (existing == null) return NotFound();
        await ValidateAsync(vm);
        if (vm.Id == User.UserId())
        {
            if (!vm.IsActive) ModelState.AddModelError(nameof(vm.IsActive), "You cannot deactivate your own account.");
            if (vm.Role != Roles.Admin) ModelState.AddModelError(nameof(vm.Role), "You cannot remove the Admin role from your own account.");
        }
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            vm.LastLoginAt = existing.LastLoginAt; vm.CreatedAt = existing.CreatedAt;
            ViewData["Title"] = "Edit User";
            ViewData["Subtitle"] = existing.FullName;
            return View("Form", vm);
        }
        await users.UpdateAsync(ToUser(vm));
        if (!string.IsNullOrWhiteSpace(vm.Password)) await users.SetPasswordAsync(vm.Id, vm.Password);
        TempData["Success"] = $"User '{vm.Username}' updated" + (string.IsNullOrWhiteSpace(vm.Password) ? "." : " and password reset.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        if (id == User.UserId())
        {
            TempData["Error"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(Index));
        }
        var u = await users.GetByIdAsync(id);
        if (u == null) return NotFound();
        if (u.IsActive && u.Role == Roles.Admin)
        {
            var activeAdmins = (await users.GetAllAsync()).Count(x => x.IsActive && x.Role == Roles.Admin);
            if (activeAdmins <= 1)
            {
                TempData["Error"] = "At least one active Admin account is required.";
                return RedirectToAction(nameof(Index));
            }
        }
        await users.ToggleActiveAsync(id);
        TempData["Success"] = $"{u.FullName} {(u.IsActive ? "deactivated" : "re-activated")}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(UserFormVm vm)
    {
        vm.Username = (vm.Username ?? "").Trim();
        if (!Roles.All.Contains(vm.Role)) ModelState.AddModelError(nameof(vm.Role), "Choose a valid role.");
        if (!string.IsNullOrEmpty(vm.Username) && await users.UsernameExistsAsync(vm.Username, vm.Id))
            ModelState.AddModelError(nameof(vm.Username), "This username is already taken.");
        if (vm.Role != Roles.FieldOfficer) vm.FieldOfficerId = null;
        if (!string.IsNullOrEmpty(vm.Password) && vm.Password != vm.ConfirmPassword)
            ModelState.AddModelError(nameof(vm.ConfirmPassword), "Passwords do not match.");
    }

    private static AppUser ToUser(UserFormVm vm) => new()
    {
        Id = vm.Id, FullName = vm.FullName.Trim(), Username = vm.Username, Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim(),
        Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim(), Role = vm.Role, FieldOfficerId = vm.FieldOfficerId, IsActive = vm.IsActive
    };

    private async Task LoadLookupsAsync()
    {
        ViewBag.FieldOfficers = (await fieldOfficers.GetAllAsync()).ToList();
    }
}
