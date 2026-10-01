using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;

namespace SWP391_G5.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly BakeryManagementDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(BakeryManagementDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Admin/Dashboard
        public IActionResult Dashboard()
        {
            return View();
        }

        // GET: /Admin/Products
        public async Task<IActionResult> Products(string? search, string? category, string? status)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .AsQueryable();

            // Lọc theo tên
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.Name.Contains(search));

            // Lọc theo danh mục
            if (!string.IsNullOrWhiteSpace(category) && int.TryParse(category, out int catId))
                query = query.Where(p => p.CategoryId == catId);

            // Lọc theo trạng thái
            if (status == "active")
                query = query.Where(p => p.IsActive);
            else if (status == "inactive")
                query = query.Where(p => !p.IsActive);

            var products = await query.OrderBy(p => p.Category.Name).ThenBy(p => p.Name).ToListAsync();
            var categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();

            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.Status = status;
            ViewBag.Categories = categories;
            ViewBag.TotalActive = products.Count(p => p.IsActive);
            ViewBag.TotalInactive = products.Count(p => !p.IsActive);
            ViewBag.TotalInStock = products.Count(p => p.StockQty > 0);
            ViewBag.TotalOutOfStock = products.Count(p => p.StockQty == 0);

            return View(products);
        }
    }
}
