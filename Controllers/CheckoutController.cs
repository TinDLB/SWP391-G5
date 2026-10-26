using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;
using SWP391_G5.Services;

namespace SWP391_G5.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly BakeryManagementDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(
            BakeryManagementDbContext context,
            INotificationService notificationService,
            ILogger<CheckoutController> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
        }

        // GET: /Checkout
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = "/Checkout" });
            }

            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.UserId == userId.Value && c.Product.IsActive)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống. Vui lòng chọn bánh trước khi đặt hàng.";
                return RedirectToAction("Index", "Cart");
            }

            var user = await _context.Users
                .Include(u => u.Addresses)
                .FirstOrDefaultAsync(u => u.UserId == userId.Value);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var savedAddresses = user.Addresses.OrderByDescending(a => a.IsDefault).ToList();
            var defaultAddress = savedAddresses.FirstOrDefault(a => a.IsDefault) ?? savedAddresses.FirstOrDefault();

            decimal subtotal = cartItems.Sum(i => i.Quantity * i.Product.Price);

            var model = new CheckoutViewModel
            {
                SelectedAddressId = defaultAddress?.AddressId,
                NewReceiverName = user.FullName,
                NewPhone = user.Phone,
                SavedAddresses = savedAddresses,
                AvailablePoints = user.LoyaltyPoints,
                Subtotal = subtotal,
                TotalAmount = subtotal,
                PointsToEarn = (int)(subtotal * 0.01m / 1000m), // Tích lũy 1% giá trị (100k = 1 điểm)
                Items = cartItems.Select(i => new CartItemSummaryViewModel
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    ImageUrl = i.Product.ImageUrl,
                    UnitPrice = i.Product.Price,
                    Quantity = i.Quantity
                }).ToList()
            };

            return View(model);
        }

        // POST: /Checkout/CheckVoucher (AJAX)
        [HttpPost]
        public async Task<IActionResult> CheckVoucher(string code, decimal subtotal)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Json(new VoucherCheckResult { Success = false, Message = "Vui lòng nhập mã giảm giá." });
            }

            var cleanCode = code.Trim().ToUpper();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == cleanCode);

            if (voucher == null)
            {
                return Json(new VoucherCheckResult { Success = false, Message = "Mã giảm giá không tồn tại." });
            }

            if (!voucher.IsActive)
            {
                return Json(new VoucherCheckResult { Success = false, Message = "Mã giảm giá đã tạm ngưng áp dụng." });
            }

            if (today < voucher.StartDate)
            {
                return Json(new VoucherCheckResult { Success = false, Message = $"Mã giảm giá chưa đến ngày áp dụng (bắt đầu từ {voucher.StartDate:dd/MM/yyyy})." });
            }

            if (today > voucher.EndDate)
            {
                return Json(new VoucherCheckResult { Success = false, Message = "Mã giảm giá đã hết hạn sử dụng." });
            }

            if (voucher.UsageLimit.HasValue && voucher.UsedCount >= voucher.UsageLimit.Value)
            {
                return Json(new VoucherCheckResult { Success = false, Message = "Mã giảm giá đã hết lượt sử dụng." });
            }

            if (subtotal < voucher.MinOrderValue)
            {
                return Json(new VoucherCheckResult { Success = false, Message = $"Đơn hàng cần tối thiểu {voucher.MinOrderValue:N0}đ để áp dụng mã này." });
            }

            decimal discount = 0;
            if (voucher.DiscountType.Equals("Percent", StringComparison.OrdinalIgnoreCase))
            {
                discount = subtotal * (voucher.DiscountValue / 100m);
                if (voucher.MaxDiscount.HasValue && discount > voucher.MaxDiscount.Value)
                {
                    discount = voucher.MaxDiscount.Value;
                }
            }
            else
            {
                discount = voucher.DiscountValue;
            }

            discount = Math.Min(discount, subtotal);
            decimal newTotal = Math.Max(0, subtotal - discount);

            return Json(new VoucherCheckResult
            {
                Success = true,
                Message = $"Áp dụng mã \"{voucher.Code}\" thành công! Giảm {discount:N0}đ.",
                VoucherId = voucher.VoucherId,
                VoucherCode = voucher.Code,
                DiscountAmount = discount,
                NewTotal = newTotal
            });
        }

        // POST: /Checkout/PlaceOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(CheckoutViewModel model)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.Users
                .Include(u => u.Addresses)
                .FirstOrDefaultAsync(u => u.UserId == userId.Value);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.UserId == userId.Value && c.Product.IsActive)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction("Index", "Cart");
            }

            // 1. Kiểm tra tồn kho của từng món
            foreach (var item in cartItems)
            {
                if (item.Product.StockQty < item.Quantity)
                {
                    TempData["ErrorMessage"] = $"Bánh \"{item.Product.Name}\" hiện chỉ còn {item.Product.StockQty} cái trong kho, không đủ số lượng đặt ({item.Quantity}).";
                    return RedirectToAction(nameof(Index));
                }
            }

            // 2. Xử lý địa chỉ nhận hàng
            int addressId = 0;
            if (model.IsNewAddress || model.SelectedAddressId == null || model.SelectedAddressId <= 0)
            {
                if (string.IsNullOrWhiteSpace(model.NewReceiverName) ||
                    string.IsNullOrWhiteSpace(model.NewPhone) ||
                    string.IsNullOrWhiteSpace(model.NewAddressLine))
                {
                    TempData["ErrorMessage"] = "Vui lòng điền đầy đủ tên người nhận, số điện thoại và địa chỉ giao hàng mới.";
                    return RedirectToAction(nameof(Index));
                }

                var newAddress = new Address
                {
                    UserId = user.UserId,
                    ReceiverName = model.NewReceiverName.Trim(),
                    Phone = model.NewPhone.Trim(),
                    AddressLine = model.NewAddressLine.Trim(),
                    IsDefault = !user.Addresses.Any()
                };

                _context.Addresses.Add(newAddress);
                await _context.SaveChangesAsync();
                addressId = newAddress.AddressId;
            }
            else
            {
                var existingAddr = user.Addresses.FirstOrDefault(a => a.AddressId == model.SelectedAddressId.Value);
                if (existingAddr == null)
                {
                    TempData["ErrorMessage"] = "Địa chỉ nhận hàng không hợp lệ.";
                    return RedirectToAction(nameof(Index));
                }
                addressId = existingAddr.AddressId;
            }

            // 3. Tính toán Subtotal
            decimal subtotal = cartItems.Sum(i => i.Quantity * i.Product.Price);
            decimal discountAmount = 0;
            Voucher? appliedVoucher = null;

            // 4. Kiểm tra Voucher nếu có
            if (!string.IsNullOrWhiteSpace(model.VoucherCode))
            {
                var cleanCode = model.VoucherCode.Trim().ToUpper();
                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                appliedVoucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == cleanCode && v.IsActive);
                if (appliedVoucher != null &&
                    today >= appliedVoucher.StartDate &&
                    today <= appliedVoucher.EndDate &&
                    subtotal >= appliedVoucher.MinOrderValue &&
                    (!appliedVoucher.UsageLimit.HasValue || appliedVoucher.UsedCount < appliedVoucher.UsageLimit.Value))
                {
                    if (appliedVoucher.DiscountType.Equals("Percent", StringComparison.OrdinalIgnoreCase))
                    {
                        var d = subtotal * (appliedVoucher.DiscountValue / 100m);
                        if (appliedVoucher.MaxDiscount.HasValue && d > appliedVoucher.MaxDiscount.Value)
                        {
                            d = appliedVoucher.MaxDiscount.Value;
                        }
                        discountAmount += d;
                    }
                    else
                    {
                        discountAmount += appliedVoucher.DiscountValue;
                    }
                    appliedVoucher.UsedCount++;
                }
            }

            // 5. Xử lý điểm thưởng thành viên (UC-29 Loyalty Points)
            int pointsUsed = 0;
            decimal pointsDiscount = 0;
            if (model.UsePoints && model.PointsToUse > 0)
            {
                // Mỗi 1 điểm = 1,000đ giảm giá
                int maxCanUse = Math.Min(user.LoyaltyPoints, model.PointsToUse);
                decimal maxAllowedDiscount = Math.Max(0, subtotal - discountAmount);
                int pointsNeededForMax = (int)Math.Ceiling(maxAllowedDiscount / 1000m);

                pointsUsed = Math.Min(maxCanUse, pointsNeededForMax);
                pointsDiscount = pointsUsed * 1000m;

                discountAmount += pointsDiscount;
                user.LoyaltyPoints -= pointsUsed;
            }

            discountAmount = Math.Min(discountAmount, subtotal);
            decimal finalTotal = Math.Max(0, subtotal - discountAmount);

            // Tích điểm thưởng cho đơn này: 1% giá trị thanh toán thực tế (100k = 1 điểm)
            int pointsEarned = (int)(finalTotal * 0.01m / 1000m);
            user.LoyaltyPoints += pointsEarned;

            // 6. Tạo mã đơn hàng duy nhất
            string orderCode = "BMS" + DateTime.UtcNow.ToString("yyMMddHHmmss") + new Random().Next(10, 99);

            var order = new Order
            {
                OrderCode = orderCode,
                Channel = "Online",
                CustomerId = user.UserId,
                AddressId = addressId,
                VoucherId = appliedVoucher?.VoucherId,
                Status = "Pending",
                PaymentMethod = model.PaymentMethod.Equals("Online", StringComparison.OrdinalIgnoreCase) ? "Online" : "COD",
                PaymentStatus = "Unpaid",
                Subtotal = subtotal,
                DiscountAmount = discountAmount,
                TotalAmount = finalTotal,
                PointsEarned = pointsEarned,
                PointsUsed = pointsUsed,
                Note = model.Note?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // 7. Tạo OrderItems và trừ tồn kho sản phẩm
            foreach (var item in cartItems)
            {
                _context.OrderItems.Add(new OrderItem
                {
                    OrderId = order.OrderId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.Product.Price
                });

                item.Product.StockQty -= item.Quantity;
            }

            // 8. Xóa giỏ hàng
            _context.CartItems.RemoveRange(cartItems);

            await _context.SaveChangesAsync();

            // 9. Gửi thông báo đặt hàng thành công (UC-08)
            await _notificationService.CreateNotificationAsync(
                user.UserId,
                "OrderPlaced",
                $"Đơn hàng #{order.OrderCode} đã được đặt thành công!",
                $"Cảm ơn bạn đã đặt bánh tại BMS. Đơn hàng trị giá {order.TotalAmount:N0}đ đang được tiệm chuẩn bị."
            );

            _logger.LogInformation("Đơn hàng #{OrderCode} được đặt thành công bởi User {UserId}, Phương thức {Method}",
                order.OrderCode, user.UserId, order.PaymentMethod);

            // 10. Chuyển hướng theo phương thức thanh toán
            if (order.PaymentMethod == "Online")
            {
                return RedirectToAction(nameof(PaymentGateway), new { orderId = order.OrderId });
            }

            return RedirectToAction(nameof(OrderSuccess), new { orderId = order.OrderId });
        }

        // GET: /Checkout/PaymentGateway?orderId=... (Mô phỏng cổng thanh toán trực tuyến / VietQR / VNPay)
        [HttpGet]
        public async Task<IActionResult> PaymentGateway(int orderId)
        {
            var userId = GetCurrentUserId();
            var order = await _context.Orders
                .Include(o => o.OrderItems).ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == userId);

            if (order == null)
            {
                return NotFound();
            }

            if (order.PaymentStatus == "Paid")
            {
                return RedirectToAction(nameof(OrderSuccess), new { orderId = order.OrderId });
            }

            return View(order);
        }

        // POST: /Checkout/ConfirmPayment (Xử lý kết quả cổng thanh toán)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int orderId, bool isSuccess)
        {
            var userId = GetCurrentUserId();
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == userId);

            if (order == null)
            {
                return NotFound();
            }

            if (isSuccess)
            {
                order.PaymentStatus = "Paid";
                order.TransactionRef = "VNP" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                await _context.SaveChangesAsync();

                await _notificationService.CreateNotificationAsync(
                    order.CustomerId!.Value,
                    "OrderPlaced",
                    $"Thanh toán trực tuyến thành công đơn #{order.OrderCode}!",
                    $"Số tiền {order.TotalAmount:N0}đ đã được thanh toán thành công qua cổng thanh toán."
                );

                TempData["SuccessMessage"] = "Thanh toán trực tuyến thành công!";
                return RedirectToAction(nameof(OrderSuccess), new { orderId = order.OrderId });
            }
            else
            {
                order.PaymentStatus = "Failed";
                await _context.SaveChangesAsync();

                TempData["ErrorMessage"] = "Giao dịch thanh toán chưa hoàn tất hoặc đã bị hủy. Bạn có thể thanh toán lại hoặc chuyển sang hình thức COD.";
                return RedirectToAction(nameof(PaymentGateway), new { orderId = order.OrderId });
            }
        }

        // GET: /Checkout/OrderSuccess?orderId=...
        [HttpGet]
        public async Task<IActionResult> OrderSuccess(int orderId)
        {
            var userId = GetCurrentUserId();
            var order = await _context.Orders
                .Include(o => o.Address)
                .Include(o => o.OrderItems).ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == userId);

            if (order == null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(order);
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
