using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

/// <summary>Farm task board. Every signed-in user can view; Viewer accounts are read-only.</summary>
public class TasksController(IFarmTaskService tasks, IFarmService farmService, IEmployeeService employeeService) : Controller
{
    private const string Editors = Roles.Admin + "," + Roles.Manager + "," + Roles.Veterinarian + "," + Roles.Accountant + "," + Roles.FieldOfficer;

    public async Task<IActionResult> Index(int? farmId, string? priority, int? assigneeId, string? category)
    {
        var vm = new TaskBoardVm
        {
            Tasks = (await tasks.GetAllAsync(farmId, priority, assigneeId, category)).ToList(),
            Farms = (await farmService.GetAllAsync()).ToList(),
            Employees = (await employeeService.GetAllAsync()).ToList(),
            FarmId = farmId, Priority = priority, AssigneeId = assigneeId, Category = category,
            CanEdit = !User.IsInRole(Roles.Viewer)
        };
        ViewData["Title"] = "Tasks";
        ViewData["Subtitle"] = "Operational task board for farms, flocks and staff";
        return View(vm);
    }

    [Authorize(Roles = Editors)]
    public async Task<IActionResult> Create(int? farmId, int? batchId, int? assigneeId, DateTime? due)
    {
        await LoadLookupsAsync();
        ViewData["Title"] = "New Task";
        ViewData["Subtitle"] = "Assign work to a farm, batch or employee";
        return View("Form", new TaskFormVm
        {
            FarmId = farmId, BatchId = batchId, AssignedToEmployeeId = assigneeId,
            DueDate = due ?? DateTime.Today.AddDays(1)
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Editors)]
    public async Task<IActionResult> Create(TaskFormVm vm)
    {
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            ViewData["Title"] = "New Task";
            ViewData["Subtitle"] = "Assign work to a farm, batch or employee";
            return View("Form", vm);
        }
        await tasks.CreateAsync(vm, User.UserName());
        TempData["Success"] = $"Task “{vm.Title}” created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var t = await tasks.GetByIdAsync(id);
        if (t == null) return NotFound();
        await LoadLookupsAsync();
        ViewBag.Task = t;
        ViewData["Title"] = User.IsInRole(Roles.Viewer) ? "Task" : "Edit Task";
        ViewData["Subtitle"] = t.Title;
        return View("Form", new TaskFormVm
        {
            Id = t.Id, Title = t.Title, Description = t.Description, FarmId = t.FarmId, BatchId = t.BatchId,
            AssignedToEmployeeId = t.AssignedToEmployeeId, Category = t.Category, Priority = t.Priority,
            Status = t.Status, DueDate = t.DueDate, CreatedBy = t.CreatedBy, CreatedAt = t.CreatedAt
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Editors)]
    public async Task<IActionResult> Edit(TaskFormVm vm)
    {
        var t = await tasks.GetByIdAsync(vm.Id);
        if (t == null) return NotFound();
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            ViewBag.Task = t;
            ViewData["Title"] = "Edit Task";
            ViewData["Subtitle"] = t.Title;
            return View("Form", vm);
        }
        await tasks.UpdateAsync(vm);
        TempData["Success"] = "Task updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Editors)]
    public async Task<IActionResult> Move(int id, string status, string? returnUrl)
    {
        if (!FarmTask.Statuses.Contains(status)) { TempData["Error"] = "Unknown task status."; }
        else
        {
            await tasks.MoveAsync(id, status);
            TempData["Success"] = status == "Done" ? "Task completed." : $"Task moved to {status}.";
        }
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Editors)]
    public async Task<IActionResult> Delete(int id)
    {
        await tasks.DeleteAsync(id);
        TempData["Success"] = "Task deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadLookupsAsync()
    {
        ViewBag.Farms = (await farmService.GetAllAsync()).ToList();
        ViewBag.Batches = (await tasks.GetActiveBatchesAsync()).ToList();
        ViewBag.Employees = (await employeeService.GetAllAsync()).ToList();
    }
}
