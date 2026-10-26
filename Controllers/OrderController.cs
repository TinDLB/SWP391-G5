using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.ViewModels;
using SWP391_G5.Services;

namespace SWP391_G5.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly BakeryManagementDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<OrderController> _logger;

        public OrderController(
            BakeryManagementDbContext context,
            INotificationService notificationService,
            ILogger<OrderController> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
        }

        // GET: /Order/Index (Lịch sử đơn hàng của tôi - UC-07)
        [HttpGet]
        public async Task<IActionResult> Index(string? status = "All")
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var baseQuery = _context.Orders
                .Where(o => o.CustomerId == userId.Value);

            var totalCount = await baseQuery.CountAsync();
            var pendingCount = await baseQuery.CountAsync(o => o.Status == "Pending");
            var preparingCount = await baseQuery.CountAsync(o => o.Status == "Preparing");
            var deliveringCount = await baseQuery.CountAsync(o => o.Status == "Delivering");
            var completedCount = await baseQuery.CountAsync(o => o.Status == "Completed");
            var cancelledCount = await baseQuery.CountAsync(o => o.Status == "Cancelled");

            var query = baseQuery;
            if (!string.IsNullOrEmpty(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(o => o.Status == status);
            }

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                .Select(o => new OrderSummaryItemViewModel
                {
                    OrderId = o.OrderId,
                    OrderCode = o.OrderCode,
                    CreatedAt = o.CreatedAt,
                    Status = o.Status,
                    PaymentMethod = o.PaymentMethod,
                    PaymentStatus = o.PaymentStatus,
                    TotalAmount = o.TotalAmount,
                    TotalItems = o.OrderItems.Sum(i => i.Quantity),
                    FirstItemName = o.OrderItems.FirstOrDefault() != null ? o.OrderItems.FirstOrDefault()!.Product.Name : "Sản phẩm",
                    FirstItemImage = o.OrderItems.FirstOrDefault() != null ? o.OrderItems.FirstOrDefault()!.Product.ImageUrl : null
                })
                .ToListAsync();

            var model = new OrderListViewModel
            {
                CurrentStatusFilter = status ?? "All",
                Orders = orders,
                TotalOrderCount = totalCount,
                PendingCount = pendingCount,
                PreparingCount = preparingCount,
                DeliveringCount = deliveringCount,
                CompletedCount = completedCount,
                CancelledCount = cancelledCount
            };

            return View(model);
        }

        // GET: /Order/Detail/5 (Chi tiết & Theo dõi tiến trình đơn hàng - UC-07)
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var order = await _context.Orders
                .Include(o => o.Address)
                .Include(o => o.Voucher)
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == userId.Value);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng hoặc bạn không có quyền xem đơn hàng này.";
                return RedirectToAction(nameof(Index));
            }

            var model = new OrderDetailViewModel
            {
                OrderId = order.OrderId,
                OrderCode = order.OrderCode,
                CreatedAt = order.CreatedAt,
                CompletedAt = order.CompletedAt,
                Status = order.Status,
                PaymentMethod = order.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                TransactionRef = order.TransactionRef,
                Subtotal = order.Subtotal,
                DiscountAmount = order.DiscountAmount,
                TotalAmount = order.TotalAmount,
                PointsEarned = order.PointsEarned,
                PointsUsed = order.PointsUsed,
                Note = order.Note,
                VoucherCode = order.Voucher?.Code,
                ReceiverName = order.Address?.ReceiverName ?? "Khách hàng",
                Phone = order.Address?.Phone ?? "",
                DeliveryAddress = order.Address?.AddressLine ?? "Nhận tại tiệm bánh",
                DeliveryStaffName = order.DeliveryStaffName,
                DeliveryStaffPhone = order.DeliveryStaffPhone,
                Items = order.OrderItems.Select(i => new OrderItemDetailViewModel
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    ImageUrl = i.Product.ImageUrl,
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity
                }).ToList()
            };

            return View(model);
        }

        // POST: /Order/CancelOrder (Hủy đơn hàng khi còn ở trạng thái Pending)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id, string? reason)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems).ThenInclude(i => i.Product)
                .Include(o => o.Voucher)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == userId.Value);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            if (!order.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Đơn hàng đã được tiệm bánh tiếp nhận và xử lý nên không thể tự hủy.";
                return RedirectToAction(nameof(Detail), new { id = order.OrderId });
            }

            // 1. Cập nhật trạng thái
            order.Status = "Cancelled";
            order.Note = string.IsNullOrWhiteSpace(order.Note)
                ? $"Khách đã hủy: {reason ?? "Không có lý do"}"
                : $"{order.Note} | Khách hủy: {reason ?? "Không có lý do"}";

            // 2. Hoàn lại tồn kho bánh
            foreach (var item in order.OrderItems)
            {
                item.Product.StockQty += item.Quantity;
            }

            // 3. Hoàn lại lượt sử dụng voucher nếu có
            if (order.Voucher != null && order.Voucher.UsedCount > 0)
            {
                order.Voucher.UsedCount--;
            }

            // 4. Hoàn lại điểm tích lũy & hủy điểm thưởng (UC-29)
            var user = await _context.Users.FindAsync(userId.Value);
            if (user != null)
            {
                if (order.PointsUsed > 0)
                {
                    user.LoyaltyPoints += order.PointsUsed; // Hoàn lại điểm đã tiêu
                }
                if (order.PointsEarned > 0)
                {
                    user.LoyaltyPoints = Math.Max(0, user.LoyaltyPoints - order.PointsEarned); // Thu hồi điểm thưởng
                }
            }

            await _context.SaveChangesAsync();

            // 5. Gửi thông báo
            await _notificationService.CreateNotificationAsync(
                userId.Value,
                "System",
                $"Đơn hàng #{order.OrderCode} đã được hủy thành công",
                "Đơn hàng của bạn đã được hủy theo yêu cầu. Điểm thưởng và voucher (nếu có) đã được hoàn trả."
            );

            _logger.LogInformation("Đơn hàng #{OrderCode} đã được hủy bởi khách hàng {UserId}", order.OrderCode, userId.Value);

            TempData["SuccessMessage"] = "Đã hủy đơn hàng thành công.";
            return RedirectToAction(nameof(Detail), new { id = order.OrderId });
        }

        private int? GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                return userId;
            }
            return null;
        }
    }
}
