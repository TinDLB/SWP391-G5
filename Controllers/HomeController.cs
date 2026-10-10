using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models;
using SWP391_G5.Models.ViewModels;

namespace SWP391_G5.Controllers;

public class HomeController : Controller
{
    private readonly BakeryManagementDbContext _context;

    public HomeController(BakeryManagementDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
        {
            return RedirectToAction("Dashboard", "Admin");
        }

        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Cashier"))
        {
            return RedirectToAction("Dashboard", "Cashier");
        }

        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Baker"))
        {
            return RedirectToAction("Index", "Baker");
        }

        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.CategoryId)
            .ToListAsync();

        var products = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.ProductId)
            .ToListAsync();

        var viewModel = new HomeViewModel
        {
            Categories = categories,
            NewCakeCollection = products.Take(8).ToList(),
            BirthdayCakes = products.Where(p => p.CategoryId == 1).Take(8).ToList(),
            BreadAndPastries = products.Where(p => p.CategoryId != 1).Take(8).ToList()
        };

        return View(viewModel);
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
