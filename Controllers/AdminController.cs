using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;

namespace SWP391_G5.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly BakeryManagementDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            BakeryManagementDbContext context,
            IWebHostEnvironment webHostEnvironment,
            ILogger<AdminController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
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
            // 1. Thống kê toàn bộ database (cố định số liệu, không bị nhảy khi lọc)
            var allProducts = await _context.Products.AsNoTracking().ToListAsync();
            ViewBag.TotalActive = allProducts.Count(p => p.IsActive);
            ViewBag.TotalInactive = allProducts.Count(p => !p.IsActive);
            ViewBag.TotalInStock = allProducts.Count(p => p.StockQty > 0);
            ViewBag.TotalOutOfStock = allProducts.Count(p => p.StockQty == 0);

            // 2. Lọc danh sách hiển thị
            var query = _context.Products
                .Include(p => p.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim().ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(category) && int.TryParse(category, out int catId))
            {
                query = query.Where(p => p.CategoryId == catId);
            }

            if (status == "active")
            {
                query = query.Where(p => p.IsActive);
            }
            else if (status == "inactive")
            {
                query = query.Where(p => !p.IsActive);
            }

            var products = await query.OrderBy(p => p.Category.Name).ThenBy(p => p.Name).ToListAsync();
            var categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();

            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.Status = status;
            ViewBag.Categories = categories;

            return View(products);
        }

        // POST: /Admin/CreateProduct
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(Product product, IFormFile? imageFile)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(product.Name))
                {
                    TempData["ErrorMessage"] = "Tên sản phẩm không được để trống!";
                    return RedirectToAction(nameof(Products));
                }

                if (imageFile != null && imageFile.Length > 0)
                {
                    product.ImageUrl = await UploadImageFileAsync(imageFile);
                }

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã thêm thành công sản phẩm: {product.Name}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm sản phẩm mới.");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi thêm sản phẩm. Vui lòng thử lại!";
            }

            return RedirectToAction(nameof(Products));
        }

        // POST: /Admin/EditProduct
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(Product product, IFormFile? imageFile)
        {
            try
            {
                var existingProduct = await _context.Products.FindAsync(product.ProductId);
                if (existingProduct == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy sản phẩm!";
                    return RedirectToAction(nameof(Products));
                }

                existingProduct.Name = product.Name.Trim();
                existingProduct.CategoryId = product.CategoryId;
                existingProduct.Price = product.Price;
                existingProduct.StockQty = product.StockQty;
                existingProduct.Description = product.Description;
                existingProduct.IsActive = product.IsActive;

                if (imageFile != null && imageFile.Length > 0)
                {
                    existingProduct.ImageUrl = await UploadImageFileAsync(imageFile);
                }

                _context.Products.Update(existingProduct);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã cập nhật sản phẩm: {existingProduct.Name}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi sửa sản phẩm ID {ProductId}", product.ProductId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật sản phẩm. Vui lòng thử lại!";
            }

            return RedirectToAction(nameof(Products));
        }

        // GET: /Admin/GetProductJson/{id}
        [HttpGet]
        public async Task<IActionResult> GetProductJson(int id)
        {
            var p = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(x => x.ProductId == id);

            if (p == null) return NotFound();

            return Json(new
            {
                p.ProductId,
                p.Name,
                p.CategoryId,
                CategoryName = p.Category?.Name,
                p.Price,
                p.StockQty,
                p.Description,
                p.ImageUrl,
                p.IsActive
            });
        }

        // POST: /Admin/ToggleStatus/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var p = await _context.Products.FindAsync(id);
            if (p == null) return NotFound();

            p.IsActive = !p.IsActive;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã chuyển trạng thái sản phẩm '{p.Name}' sang {(p.IsActive ? "Đang bán" : "Tạm ngưng")}";
            return RedirectToAction(nameof(Products));
        }

        // Helper: Tải ảnh sản phẩm lên thư mục wwwroot/uploads/products
        private async Task<string> UploadImageFileAsync(IFormFile imageFile)
        {
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var extension = Path.GetExtension(imageFile.FileName).ToLower();
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            return $"/uploads/products/{uniqueFileName}";
        }
    }
}
