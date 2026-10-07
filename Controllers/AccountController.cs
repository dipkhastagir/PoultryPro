using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;
using PoultryPro.Models.ViewModels;

namespace PoultryPro.Controllers;

public class AccountController(IUserService users, IAuditService audit) : Controller
{
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Dashboard");
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var user = await users.AuthenticateAsync(vm.Username.Trim(), vm.Password);
        if (user == null)
        {
            await audit.LogAsync(vm.Username, "LoginFailed", "Account", null, null, HttpContext.Connection.RemoteIpAddress?.ToString());
            ModelState.AddModelError("", "Invalid username or password, or the account is disabled.");
            return View(vm);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new("FullName", user.FullName)
        };
        if (user.FieldOfficerId.HasValue) claims.Add(new Claim("FieldOfficerId", user.FieldOfficerId.Value.ToString()));

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = vm.RememberMe });
        await audit.LogAsync(user.Username, "Login", "Account", user.Id.ToString(), null, HttpContext.Connection.RemoteIpAddress?.ToString());

        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl)) return Redirect(vm.ReturnUrl);
        return user.Role == Roles.FieldOfficer ? RedirectToAction("Index", "Visit") : RedirectToAction("Index", "Dashboard");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await audit.LogAsync(User.UserName(), "Logout", "Account", User.UserId().ToString());
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        ViewData["Title"] = "Access denied";
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    public async Task<IActionResult> Profile()
    {
        var u = await users.GetByIdAsync(User.UserId());
        if (u == null) return RedirectToAction(nameof(Login));
        ViewData["Title"] = "My Profile";
        ViewData["Subtitle"] = "Account details and security";
        return View(new ProfileVm
        {
            Id = u.Id, Username = u.Username, Role = u.Role, FullName = u.FullName,
            Email = u.Email, Phone = u.Phone, LastLoginAt = u.LastLoginAt, CreatedAt = u.CreatedAt
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileVm vm)
    {
        // Username / Role are display-only (not posted); don't let their implicit [Required] fail the save
        ModelState.Remove(nameof(vm.Username));
        ModelState.Remove(nameof(vm.Role));
        if (!ModelState.IsValid)
        {
            var u = await users.GetByIdAsync(User.UserId());
            if (u != null) { vm.Username = u.Username; vm.Role = u.Role; vm.LastLoginAt = u.LastLoginAt; vm.CreatedAt = u.CreatedAt; }
            ViewData["Title"] = "My Profile";
            return View(vm);
        }
        await users.UpdateProfileAsync(User.UserId(), vm.FullName, vm.Email, vm.Phone);
        TempData["Success"] = "Profile updated. Sign in again to refresh your display name.";
        return RedirectToAction(nameof(Profile));
    }

    public IActionResult ChangePassword()
    {
        ViewData["Title"] = "Change Password";
        ViewData["Subtitle"] = "Use a strong password you do not use elsewhere";
        return View(new ChangePasswordVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordVm vm)
    {
        ViewData["Title"] = "Change Password";
        if (!ModelState.IsValid) return View(vm);
        var check = await users.AuthenticateAsync(User.UserName(), vm.CurrentPassword);
        if (check == null)
        {
            ModelState.AddModelError(nameof(vm.CurrentPassword), "Current password is incorrect.");
            return View(vm);
        }
        await users.SetPasswordAsync(User.UserId(), vm.NewPassword);
        await audit.LogAsync(User.UserName(), "ChangePassword", "Account", User.UserId().ToString());
        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction(nameof(Profile));
    }
}
