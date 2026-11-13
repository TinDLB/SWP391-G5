using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;
using SWP391_G5.Services;

namespace SWP391_G5.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly BakeryManagementDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<AdminController> _logger;
        private readonly INotificationService _notificationService;

        public AdminController(
            BakeryManagementDbContext context,
            IWebHostEnvironment webHostEnvironment,
            ILogger<AdminController> logger,
            INotificationService notificationService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> Report(DateTime? from, DateTime? to)
        {
            var start = (from?.Date ?? DateTime.Today.AddDays(-29));
            var end = (to?.Date ?? DateTime.Today).AddDays(1);
            var orders = _context.Orders.Where(o => o.CreatedAt >= start && o.CreatedAt < end);
            var model = new AdminReportViewModel
            {
                From = start,
                To = end.AddDays(-1),
                OrderCount = await orders.CountAsync(),
                CompletedOrderCount = await orders.CountAsync(o => o.Status == "Completed"),
                CancelledOrderCount = await orders.CountAsync(o => o.Status == "Cancelled"),
                NewUserCount = await _context.Users.CountAsync(u => u.CreatedAt >= start && u.CreatedAt < end),
                Revenue = await orders.Where(o => o.Status == "Completed" && o.PaymentStatus == "Paid")
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0
            };
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> IngredientUsage(DateTime? from, DateTime? to, string period = "day")
        {
            period = period.Equals("month", StringComparison.OrdinalIgnoreCase) ? "month" : "day";
            var selectedFrom = (from ?? DateTime.Today).Date;
            var selectedTo = (to ?? selectedFrom).Date;

            if (period == "month")
            {
                selectedFrom = new DateTime(selectedFrom.Year, selectedFrom.Month, 1);
                selectedTo = new DateTime(selectedTo.Year, selectedTo.Month, 1)
                    .AddMonths(1).AddDays(-1);
            }
            else if (selectedTo < selectedFrom)
            {
                (selectedFrom, selectedTo) = (selectedTo, selectedFrom);
            }

            var endExclusive = selectedTo.AddDays(1);
            var usage = await _context.InventoryTransactions
                .AsNoTracking()
                .Where(t => t.TxnType == "ProductionUse" &&
                            t.CreatedAt >= selectedFrom && t.CreatedAt < endExclusive &&
                            t.QtyChange < 0)
                .GroupBy(t => new
                {
                    t.IngredientId,
                    t.Ingredient.Name,
                    t.Ingredient.Unit,
                    t.Ingredient.StockQty,
                    t.Ingredient.MinThreshold
                })
                .Select(g => new IngredientUsageItemViewModel
                {
                    IngredientId = g.Key.IngredientId,
                    IngredientName = g.Key.Name,
                    Unit = g.Key.Unit,
                    UsedQty = g.Sum(t => -t.QtyChange),
                    TaskCount = g.Select(t => t.RefId).Where(id => id.HasValue).Distinct().Count(),
                    CurrentStock = g.Key.StockQty,
                    MinThreshold = g.Key.MinThreshold
                })
                .OrderByDescending(item => item.UsedQty)
                .ToListAsync();

            return View(new IngredientUsageReportViewModel
            {
                From = selectedFrom,
                To = selectedTo,
                Period = period,
                TotalUsage = usage.Sum(item => item.UsedQty),
                Items = usage
            });
        }

        [HttpGet]
        public async Task<IActionResult> ExportReport(DateTime? from, DateTime? to)
        {
            var start = from?.Date ?? DateTime.Today.AddDays(-29);
            var end = (to?.Date ?? DateTime.Today).AddDays(1);
            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.CreatedAt >= start && o.CreatedAt < end)
                .OrderBy(o => o.CreatedAt)
                .Select(o => new
                {
                    o.OrderCode,
                    o.Channel,
                    o.Status,
                    o.PaymentStatus,
                    o.TotalAmount,
                    o.CreatedAt
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("OrderCode,Channel,Status,PaymentStatus,TotalAmount,CreatedAt");
            foreach (var order in orders)
            {
                csv.AppendLine(string.Join(",",
                    Csv(order.OrderCode),
                    Csv(order.Channel),
                    Csv(order.Status),
                    Csv(order.PaymentStatus),
                    order.TotalAmount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    order.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")));
            }

            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"bms-report-{start:yyyyMMdd}-{end.AddDays(-1):yyyyMMdd}.csv");
        }

        private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

        // GET: /Admin/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.Today;
            var model = new AdminDashboardViewModel
            {
                ProductCount = await _context.Products.CountAsync(p => p.IsActive),
                TodayOrderCount = await _context.Orders.CountAsync(o => o.CreatedAt >= today),
                ActiveUserCount = await _context.Users.CountAsync(u => u.IsActive),
                TodayRevenue = await _context.Orders
                    .Where(o => o.CreatedAt >= today && o.Status == "Completed" && o.PaymentStatus == "Paid")
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0
            };

            model.LowStockItems = await _context.Ingredients
                .Where(i => i.IsActive && i.StockQty <= i.MinThreshold)
                .OrderBy(i => i.StockQty / (i.MinThreshold == 0 ? 1 : i.MinThreshold))
                .Select(i => new LowStockItemViewModel
                {
                    IngredientId = i.IngredientId,
                    Name = i.Name,
                    Unit = i.Unit,
                    StockQty = i.StockQty,
                    MinThreshold = i.MinThreshold
                }).ToListAsync();
            model.LowStockCount = model.LowStockItems.Count;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Notifications(bool unreadOnly = false)
        {
            var adminId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
            var query = _context.Notifications.Where(n => n.UserId == adminId).AsNoTracking();
            if (unreadOnly) query = query.Where(n => !n.IsRead);
            var notifications = await query.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();
            ViewBag.UnreadOnly = unreadOnly;
            return View(notifications);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationRead(int id)
        {
            var adminId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == adminId);
            if (notification != null) { notification.IsRead = true; await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Notifications));
        }

        [HttpGet]
        public async Task<IActionResult> Orders(string? status, string? channel, string? search)
        {
            var query = _context.Orders.Include(o => o.Customer).AsNoTracking();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status);
            if (!string.IsNullOrWhiteSpace(channel)) query = query.Where(o => o.Channel == channel);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(o => o.OrderCode.Contains(term) ||
                    (o.Customer != null && o.Customer.FullName.Contains(term)));
            }

            var model = new OrderMonitoringViewModel
            {
                Status = status,
                Channel = channel,
                Search = search,
                Orders = await query.OrderByDescending(o => o.CreatedAt).Select(o => new OrderMonitoringItemViewModel
                {
                    OrderId = o.OrderId, OrderCode = o.OrderCode, Channel = o.Channel,
                    Status = o.Status, PaymentStatus = o.PaymentStatus,
                    CustomerName = o.Customer == null ? "Khách vãng lai" : o.Customer.FullName,
                    TotalAmount = o.TotalAmount, CreatedAt = o.CreatedAt
                }).ToListAsync()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLowStockNotifications()
        {
            var lowStock = await _context.Ingredients.Where(i => i.IsActive && i.StockQty <= i.MinThreshold).ToListAsync();
            var adminIds = await _context.Users.Where(u => u.Role == "Admin" && u.IsActive).Select(u => u.UserId).ToListAsync();
            var today = DateTime.Today;
            foreach (var ingredient in lowStock)
            foreach (var adminId in adminIds)
            {
                var exists = await _context.Notifications.AnyAsync(n => n.UserId == adminId &&
                    n.NotificationType == "LowStock" && n.Message!.Contains($"#{ingredient.IngredientId}") && n.CreatedAt >= today);
                if (!exists) _context.Notifications.Add(new Notification
                {
                    UserId = adminId, NotificationType = "LowStock",
                    Title = $"Tồn kho thấp: {ingredient.Name}",
                    Message = $"#{ingredient.IngredientId}: còn {ingredient.StockQty} {ingredient.Unit}, ngưỡng {ingredient.MinThreshold}.",
                    CreatedAt = DateTime.Now
                });
            }
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã tạo thông báo cho các nguyên liệu dưới ngưỡng.";
            return RedirectToAction(nameof(Dashboard));
        }

        // Lấy danh sách sản phẩm với các bộ lọc: search, category, status
        public async Task<IActionResult> Products(string? search, string? category, string? status)
        {
            // 1. Thống kê toàn bộ database (cố định số liệu, không bị nhảy khi lọc)
            var allProducts = await _context.Products.AsNoTracking().ToListAsync(); //asnotracking để tránh tracking các entity, giúp tăng hiệu suất khi chỉ đọc dữ liệu
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
            } // Lọc theo từ khóa tìm kiếm, bỏ qua khoảng trắng và không phân biệt hoa thường

            if (!string.IsNullOrWhiteSpace(category) && int.TryParse(category, out int catId))
            {
                query = query.Where(p => p.CategoryId == catId);
            } // Lọc theo danh mục, chỉ áp dụng nếu category là số hợp lệ

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

        // Thêm sản phẩm mới vào database, có thể kèm theo ảnh sản phẩm
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

                // Tồn kho do module kho quản lý, không nhận từ form quản lý bánh.
                product.StockQty = 0;

                if (imageFile != null && imageFile.Length > 0)
                {
                    product.ImageUrl = await UploadImageFileAsync(imageFile);
                } // Nếu không có ảnh, ImageUrl sẽ là null, và view sẽ hiển thị ảnh mặc định

                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                await _notificationService.NotifyAdminsAsync("ProductCreated", "Đã thêm sản phẩm", $"Sản phẩm '{product.Name}' vừa được thêm vào hệ thống.");

                TempData["SuccessMessage"] = $"Đã thêm thành công sản phẩm: {product.Name}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm sản phẩm mới.");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi thêm sản phẩm. Vui lòng thử lại!";
            }

            return RedirectToAction(nameof(Products));
        }

        // Chỉnh sửa thông tin sản phẩm, có thể kèm theo thay đổi ảnh sản phẩm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(Product product, IFormFile? imageFile)
        {
            try
            {
                var existingProduct = await _context.Products.FindAsync(product.ProductId); // Tìm sản phẩm trong database theo ProductId
                if (existingProduct == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy sản phẩm!";
                    return RedirectToAction(nameof(Products));
                }

                existingProduct.Name = product.Name.Trim(); // Cập nhật tên sản phẩm, loại bỏ khoảng trắng thừa
                existingProduct.CategoryId = product.CategoryId;
                existingProduct.Price = product.Price;
                existingProduct.Description = product.Description;
                existingProduct.IsActive = product.IsActive;

                if (imageFile != null && imageFile.Length > 0)
                {
                    existingProduct.ImageUrl = await UploadImageFileAsync(imageFile);
                }

                _context.Products.Update(existingProduct);
                await _context.SaveChangesAsync();
                await _notificationService.NotifyAdminsAsync("ProductUpdated", "Đã chỉnh sửa sản phẩm", $"Sản phẩm '{existingProduct.Name}' vừa được cập nhật.");

                TempData["SuccessMessage"] = $"Đã cập nhật sản phẩm: {existingProduct.Name}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi sửa sản phẩm ID {ProductId}", product.ProductId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật sản phẩm. Vui lòng thử lại!";
            }

            return RedirectToAction(nameof(Products));
        }

        // Hàm trả về thông tin sản phẩm dưới dạng JSON, dùng cho AJAX khi chỉnh sửa sản phẩm
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

        // Hàm chuyển trạng thái sản phẩm (active/inactive) bằng AJAX, trả về JSON
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var p = await _context.Products.FindAsync(id);
            if (p == null) return NotFound();

            p.IsActive = !p.IsActive;
            await _context.SaveChangesAsync();
            await _notificationService.NotifyAdminsAsync("ProductStatusChanged", "Đã thay đổi trạng thái sản phẩm", $"Sản phẩm '{p.Name}' đã chuyển sang {(p.IsActive ? "Đang bán" : "Tạm ngưng")}.");

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
