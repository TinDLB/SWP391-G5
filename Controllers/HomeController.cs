using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SWP391_G5.Models;

namespace SWP391_G5.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
        {
            return RedirectToAction("Dashboard", "Admin");
        }
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
