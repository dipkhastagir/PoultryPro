using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PoultryPro.Infrastructure;

namespace PoultryPro.Controllers;

public class HomeController(DbStatus status) : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "Dashboard");

    [AllowAnonymous]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        ViewData["Title"] = "Something went wrong";
        ViewBag.RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        ViewBag.Path = feature?.Path;
        ViewBag.Message = feature?.Error.Message;
        return View();
    }

    public IActionResult About()
    {
        ViewData["Title"] = "About PoultryPro";
        ViewData["Subtitle"] = "Platform architecture, modules and research methods";
        ViewBag.Server = status.ServerUsed;
        ViewBag.Log = status.Log.ToList();
        return View();
    }
}
