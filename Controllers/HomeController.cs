using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SWP391_G5.Models;
using SWP391_G5.Models.ViewModels;
using SWP391_G5.Data;
using Microsoft.EntityFrameworkCore;

namespace SWP391_G5.Controllers;

public class HomeController : Controller
{
    private readonly BakeryManagementDbContext _context;

    public HomeController(BakeryManagementDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? categoryId, string? search, string? sort, int page = 1)
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
        {
            return RedirectToAction("Dashboard", "Admin");
        }

        if (User.IsInRole("Cashier")) return RedirectToAction("Dashboard", "Cashier");

        var categories = await _context.Categories.AsNoTracking()
            .Where(c => c.IsActive).OrderBy(c => c.CategoryId).ToListAsync();
        if (categoryId.HasValue && !categories.Any(c => c.CategoryId == categoryId))
        {
            return NotFound();
        }

        var products = _context.Products.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.Category.IsActive);
        if (categoryId.HasValue)
        {
            products = products.Where(p => p.CategoryId == categoryId.Value);
        }

        search = search?.Trim();
        string? searchError = null;
        if (Request.Query.ContainsKey("search") && string.IsNullOrWhiteSpace(search))
        {
            searchError = "Vui lòng nhập tên sản phẩm, không chỉ nhập khoảng trắng.";
            products = products.Where(p => false);
        }
        else if (!string.IsNullOrEmpty(search))
        {
            products = products.Where(p => p.Name.Contains(search));
        }

        sort = sort is "price-asc" or "price-desc" or "newest" ? sort : "";
        products = sort switch
        {
            "price-asc" => products.OrderBy(p => p.Price).ThenBy(p => p.ProductId),
            "price-desc" => products.OrderByDescending(p => p.Price).ThenBy(p => p.ProductId),
            "newest" => products.OrderByDescending(p => p.ProductId),
            _ => products.OrderBy(p => p.ProductId)
        };

        const int pageSize = 8;
        var totalProducts = await products.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalProducts / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        return View(new HomeViewModel
        {
            Categories = categories,
            Products = await products.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(),
            CurrentPage = page,
            TotalPages = totalPages,
            Search = search,
            Sort = sort,
            SearchError = searchError,
            SelectedCategoryId = categoryId
        });
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
