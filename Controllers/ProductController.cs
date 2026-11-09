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
        public async Task<IActionResult> Index(
            string? keyword,
            int? categoryId,
            decimal? minPrice,
            decimal? maxPrice,
            string? priceRange,
            string? sort,
            int page = 1)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive && p.Category.IsActive)
                .AsQueryable();

            // 1. Search keyword
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var term = keyword.Trim().ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(term) || (p.Description != null && p.Description.ToLower().Contains(term)));
            }

            // 2. Filter by Category
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            // 3. Filter by Price Range (quick filter or custom)
            if (!string.IsNullOrEmpty(priceRange))
            {
                switch (priceRange)
                {
                    case "under50":
                        minPrice = 0;
                        maxPrice = 50000;
                        break;
                    case "50to100":
                        minPrice = 50000;
                        maxPrice = 100000;
                        break;
                    case "100to200":
                        minPrice = 100000;
                        maxPrice = 200000;
                        break;
                    case "200to300":
                        minPrice = 200000;
                        maxPrice = 300000;
                        break;
                    case "300to400":
                        minPrice = 300000;
                        maxPrice = 400000;
                        break;
                    case "over400":
                        minPrice = 400000;
                        maxPrice = null;
                        break;
                }
            }

            if (minPrice.HasValue && minPrice.Value > 0)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue && maxPrice.Value > 0)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            // 4. Sort
            query = sort switch
            {
                "price_asc" => query.OrderBy(p => p.Price).ThenBy(p => p.ProductId),
                "price_desc" => query.OrderByDescending(p => p.Price).ThenBy(p => p.ProductId),
                "name_asc" => query.OrderBy(p => p.Name).ThenBy(p => p.ProductId),
                _ => query.OrderByDescending(p => p.ProductId)
            };

            // 5. Pagination
            const int pageSize = 12;
            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var products = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.CategoryId).ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.Keyword = keyword?.Trim();
            ViewBag.CategoryId = categoryId;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.PriceRange = priceRange;
            ViewBag.Sort = sort ?? "";
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

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
                .Include(p => p.Category)
                .Where(p => p.CategoryId == product.CategoryId && p.ProductId != product.ProductId && p.IsActive)
                .Take(4)
                .ToListAsync();

            ViewBag.RelatedProducts = relatedProducts;
            return View(product);
        }
    }
}
