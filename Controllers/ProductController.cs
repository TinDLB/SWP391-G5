using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;

namespace SWP391_G5.Controllers
{
    public class ProductController : Controller
    {
        private readonly BakeryManagementDbContext _context;

        public ProductController(BakeryManagementDbContext context)
        {
            _context = context;
        }

        // GET: /Product/Index (UC-03: Search & Filter Products)
        public async Task<IActionResult> Index(string? keyword, int? categoryId, decimal? minPrice, decimal? maxPrice, string? sort)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var term = keyword.Trim().ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(term) || (p.Description != null && p.Description.ToLower().Contains(term)));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (minPrice.HasValue)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            query = sort switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "name_asc" => query.OrderBy(p => p.Name),
                _ => query.OrderByDescending(p => p.ProductId)
            };

            var categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
            var products = await query.ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.Keyword = keyword;
            ViewBag.CategoryId = categoryId;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.Sort = sort;

            return View(products);
        }

        // GET: /Product/Detail/5
        public async Task<IActionResult> Detail(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == id && p.IsActive);

            if (product == null)
            {
                return NotFound();
            }

            var relatedProducts = await _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.ProductId != product.ProductId && p.IsActive)
                .Take(4)
                .ToListAsync();

            ViewBag.RelatedProducts = relatedProducts;
            return View(product);
        }
    }
}
