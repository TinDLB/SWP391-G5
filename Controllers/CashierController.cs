using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;
using SWP391_G5.Services;
using System.Security.Claims;

namespace SWP391_G5.Controllers
{
    [Authorize(Roles = "Cashier")]
    public class CashierController : Controller
    {
        private readonly BakeryManagementDbContext _context;
        private readonly IPasswordService _passwordService;
        private readonly ILogger<CashierController> _logger;
        private readonly INotificationService _notificationService;

        public CashierController(
            BakeryManagementDbContext context,
            IPasswordService passwordService,
            ILogger<CashierController> logger,
            INotificationService notificationService)
        {
            _context = context;
            _passwordService = passwordService;
            _logger = logger;
            _notificationService = notificationService;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // =====================================================================
        // UC-10: Cashier Dashboard
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var todayStart = DateTime.Today;
            var tomorrowStart = todayStart.AddDays(1);

            // Doanh thu hôm nay: đơn Completed có CompletedAt trong ngày (cả Online lẫn Offline)
            var completedToday = _context.Orders.AsNoTracking()
                .Where(o => o.Status == "Completed"
                         && o.CompletedAt >= todayStart
                         && o.CompletedAt < tomorrowStart);

            var onlineOrders = _context.Orders.AsNoTracking()
                .Where(o => o.Channel == "Online");

            var model = new CashierDashboardViewModel
            {
                TodayRevenue = await completedToday.SumAsync(o => (decimal?)o.TotalAmount) ?? 0,
                TodayCompletedOrders = await completedToday.CountAsync(),
                PendingOnlineOrders = await onlineOrders.CountAsync(o => o.Status == "Pending"),
                PreparingOnlineOrders = await onlineOrders.CountAsync(o => o.Status == "Preparing"),
                DeliveringOnlineOrders = await onlineOrders.CountAsync(o => o.Status == "Delivering"),
                RecentPendingOrders = await onlineOrders
                    .Where(o => o.Status == "Pending")
                    .OrderBy(o => o.CreatedAt)              // đơn chờ lâu nhất lên đầu
                    .Take(5)
                    .Select(o => new PendingOrderItemViewModel
                    {
                        OrderId = o.OrderId,
                        OrderCode = o.OrderCode,
                        CustomerName = o.Customer != null ? o.Customer.FullName : "Khách vãng lai",
                        CreatedAt = o.CreatedAt,
                        ItemCount = o.OrderItems.Count,
                        TotalAmount = o.TotalAmount,
                        PaymentMethod = o.PaymentMethod
                    })
                    .ToListAsync()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CounterOrder()
        {
            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive && p.StockQty > 0)
                .OrderBy(p => p.Name)
                .AsNoTracking()
                .ToListAsync();
            return View(new CashierCounterOrderViewModel
            {
                Items = new() { new CashierCounterOrderItemViewModel() }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CounterOrder(CashierCounterOrderViewModel model)
        {
            var items = model.Items.Where(i => i.ProductId > 0 && i.Quantity > 0).ToList();
            if (items.Count == 0)
                ModelState.AddModelError(nameof(model.Items), "Vui lòng thêm ít nhất một sản phẩm.");
            if (model.PaymentMethod != "Cash")
                ModelState.AddModelError(nameof(model.PaymentMethod), "Đơn offline chỉ thanh toán bằng tiền mặt.");

            var productIds = items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products.Where(p => productIds.Contains(p.ProductId) && p.IsActive).ToDictionaryAsync(p => p.ProductId);
            foreach (var item in items)
            {
                if (!products.TryGetValue(item.ProductId, out var product))
                    ModelState.AddModelError(nameof(model.Items), "Sản phẩm không tồn tại hoặc đã ngừng bán.");
                else if (item.Quantity > product.StockQty)
                    ModelState.AddModelError(nameof(model.Items), $"Sản phẩm {product.Name} chỉ còn {product.StockQty}.");
            }

            Voucher? voucher = null;
            if (!string.IsNullOrWhiteSpace(model.VoucherCode))
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == model.VoucherCode.Trim().ToUpper() && v.IsActive);
                if (voucher == null || today < voucher.StartDate || today > voucher.EndDate || (voucher.UsageLimit.HasValue && voucher.UsedCount >= voucher.UsageLimit.Value))
                    ModelState.AddModelError(nameof(model.VoucherCode), "Voucher không hợp lệ hoặc đã hết lượt sử dụng.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Products = await _context.Products.Where(p => p.IsActive && p.StockQty > 0).OrderBy(p => p.Name).AsNoTracking().ToListAsync();
                model.Items = model.Items.Count == 0 ? new() { new CashierCounterOrderItemViewModel() } : model.Items;
                return View(model);
            }

            var subtotal = items.Sum(item => products[item.ProductId].Price * item.Quantity);
            var discount = voucher == null ? 0 : voucher.DiscountType == "Percent"
                ? Math.Min(subtotal * voucher.DiscountValue / 100, voucher.MaxDiscount ?? decimal.MaxValue)
                : Math.Min(subtotal, voucher.DiscountValue);
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var order = new Order
            {
                OrderCode = $"POS-{DateTime.Now:yyyyMMddHHmmssfff}", Channel = "Offline", CashierId = CurrentUserId,
                VoucherId = voucher?.VoucherId, Status = "Completed", PaymentMethod = "Cash",
                PaymentStatus = "Paid", Subtotal = subtotal, DiscountAmount = discount, TotalAmount = subtotal - discount,
                Note = "Đơn bán tại quầy",
                CreatedAt = DateTime.Now, CompletedAt = DateTime.Now
            };
            foreach (var item in items)
            {
                var product = products[item.ProductId];
                product.StockQty -= item.Quantity;
                order.OrderItems.Add(new OrderItem { ProductId = product.ProductId, Quantity = item.Quantity, UnitPrice = product.Price });
            }
            if (voucher != null) voucher.UsedCount++;
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["SuccessMessage"] = $"Đã tạo đơn tại quầy {order.OrderCode}.";
            return RedirectToAction(nameof(OrderDetails), new { id = order.OrderId });
        }

        [HttpGet]
        public async Task<IActionResult> Orders(string? status, string? channel, string? search)
        {
            // Luồng: Cashier gửi bộ lọc từ màn hình -> truy vấn Orders -> JOIN Customer
            // -> chiếu sang ViewModel -> trả danh sách cho Views/Cashier/Orders.cshtml.
            // Dữ liệu đọc từ Orders và Users (thông qua quan hệ Orders.CustomerId).
            var query = _context.Orders.Include(o => o.Customer).AsNoTracking();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status);
            if (!string.IsNullOrWhiteSpace(channel)) query = query.Where(o => o.Channel == channel);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(o => o.OrderCode.Contains(term) ||
                    (o.Customer != null && o.Customer.FullName.Contains(term)));
            }

            var model = new CashierOrderListViewModel
            {
                Status = status, Channel = channel, Search = search,
                Orders = await query.OrderByDescending(o => o.CreatedAt).Select(o => new CashierOrderListItemViewModel
                {
                    OrderId = o.OrderId, OrderCode = o.OrderCode, Channel = o.Channel,
                    CustomerName = o.Customer == null ? "Khách vãng lai" : o.Customer.FullName,
                    Status = o.Status, PaymentStatus = o.PaymentStatus, PaymentMethod = o.PaymentMethod,
                    TotalAmount = o.TotalAmount, CreatedAt = o.CreatedAt
                }).ToListAsync()
            };
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> OrderDetails(int id)
        {
            // Luồng chi tiết: nhận OrderId -> đọc thông tin đơn, khách hàng, địa chỉ,
            // hóa đơn và các dòng sản phẩm -> trả về màn hình chi tiết.
            // Nguồn dữ liệu: Orders, Users, Addresses, Invoices, OrderItems, Products.
            var model = await BuildOrderDetailsAsync(id);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(int id, string status)
        {
            // Luồng cập nhật trạng thái:
            // 1. Mở transaction Serializable để tránh hai Cashier cập nhật cùng một đơn.
            // 2. Đọc đơn từ Orders và kiểm tra trạng thái hiện tại.
            // 3. Nếu chuyển trạng thái hợp lệ thì cập nhật Orders.Status.
            // 4. Khi hoàn tất, cập nhật CompletedAt; COD được chuyển sang Paid.
            // 5. Khi hủy đơn đã thanh toán, PaymentStatus chuyển sang Refunded.
            // 6. SaveChanges ghi dữ liệu vào Orders rồi commit transaction.
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();
            if (!IsValidStatusTransition(order.Status, status))
            {
                TempData["ErrorMessage"] = $"Không thể chuyển đơn từ '{order.Status}' sang '{status}'.";
                return RedirectToAction(nameof(OrderDetails), new { id });
            }

            order.Status = status;
            if (status == "Completed")
            {
                order.CompletedAt = DateTime.Now;
                if (order.PaymentMethod == "COD") order.PaymentStatus = "Paid";
            }
            if (status == "Cancelled" && order.PaymentStatus == "Paid") order.PaymentStatus = "Refunded";
            order.CashierId = CurrentUserId;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            await _notificationService.NotifyAdminsAsync("System", "Đơn hàng được cập nhật", $"Đơn hàng {order.OrderCode} chuyển sang trạng thái {status}.");
            if (order.CustomerId.HasValue)
            {
                await _notificationService.CreateNotificationAsync(
                    order.CustomerId.Value,
                    "System",
                    $"Đơn hàng {order.OrderCode} đã được cập nhật",
                    $"Đơn hàng của bạn hiện ở trạng thái: {GetOrderStatusText(status)}.");
            }
            TempData["SuccessMessage"] = "Đã cập nhật trạng thái đơn hàng.";
            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> AssignDelivery(int id)
        {
            // Chỉ đọc đơn Online đang Delivering từ Orders.
            // Form được nạp lại DeliveryStaffName và DeliveryStaffPhone để Cashier sửa thông tin giao hàng.
            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();
            if (order.Channel != "Online" || order.Status != "Delivering")
            {
                TempData["ErrorMessage"] = "Chỉ phân công giao hàng cho đơn online đang ở trạng thái Delivering.";
                return RedirectToAction(nameof(OrderDetails), new { id });
            }
            return View(new DeliveryAssignmentViewModel { OrderId = id, DeliveryStaffName = order.DeliveryStaffName ?? "", DeliveryStaffPhone = order.DeliveryStaffPhone ?? "" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDelivery(DeliveryAssignmentViewModel model)
        {
            // Khi submit, tìm lại đơn trong Orders, kiểm tra điều kiện giao hàng,
            // sau đó lưu người giao vào DeliveryStaffName/DeliveryStaffPhone của Orders.
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == model.OrderId);
            if (order == null) return NotFound();
            if (order.Channel != "Online" || order.Status != "Delivering") ModelState.AddModelError("", "Đơn phải là online và đang giao hàng.");
            if (!ModelState.IsValid) return View(model);
            order.DeliveryStaffName = model.DeliveryStaffName.Trim();
            order.DeliveryStaffPhone = model.DeliveryStaffPhone.Trim();
            order.CashierId = CurrentUserId;
            await _context.SaveChangesAsync();
            await _notificationService.NotifyAdminsAsync("DeliveryAssigned", "Đã cập nhật người giao hàng", $"Đơn hàng {order.OrderCode}: {order.DeliveryStaffName} - {order.DeliveryStaffPhone}.");
            if (order.CustomerId.HasValue)
            {
                await _notificationService.CreateNotificationAsync(
                    order.CustomerId.Value,
                    "DeliveryAssigned",
                    $"Đơn hàng {order.OrderCode} đã có người giao hàng",
                    $"Người giao hàng: {order.DeliveryStaffName}. Số điện thoại: {order.DeliveryStaffPhone}.");
            }
            TempData["SuccessMessage"] = "Đã cập nhật thông tin người giao hàng.";
            return RedirectToAction(nameof(OrderDetails), new { id = model.OrderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateInvoice(int id)
        {
            // Hóa đơn chỉ được lập sau khi đơn đã Completed và PaymentStatus là Paid.
            // Invoice.OrderId liên kết 1-1 với Orders.OrderId; InvoiceNo được unique trong database.
            // Nếu đã có Invoice thì không tạo bản ghi mới để tránh hóa đơn trùng.
            var order = await _context.Orders.Include(o => o.Invoice).FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();
            if (order.Status != "Completed" || order.PaymentStatus != "Paid")
            {
                TempData["ErrorMessage"] = "Chỉ có thể lập hóa đơn cho đơn đã hoàn tất và thanh toán thành công.";
                return RedirectToAction(nameof(OrderDetails), new { id });
            }
            if (order.Invoice == null)
            {
                order.Invoice = new Invoice { InvoiceNo = $"HD-{DateTime.Now:yyyyMMdd}-{order.OrderId:D6}", IssuedBy = CurrentUserId, IssuedAt = DateTime.Now };
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã lập hóa đơn {order.Invoice.InvoiceNo}.";
            }
            else TempData["InfoMessage"] = $"Đơn hàng đã có hóa đơn {order.Invoice.InvoiceNo}.";
            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Invoice(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Address)
                .Include(o => o.Invoice)
                .Include(o => o.OrderItems).ThenInclude(i => i.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == id && o.Status == "Completed" && o.PaymentStatus == "Paid");

            if (order?.Invoice == null) return NotFound();
            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadInvoice(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Address)
                .Include(o => o.Invoice)
                .Include(o => o.OrderItems).ThenInclude(i => i.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == id && o.Status == "Completed" && o.PaymentStatus == "Paid");

            if (order?.Invoice == null) return NotFound();

            var html = new StringBuilder();
            html.AppendLine("<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\">");
            html.AppendLine($"<title>Hóa đơn {WebUtility.HtmlEncode(order.Invoice.InvoiceNo)}</title>");
            html.AppendLine("<style>body{font-family:Arial,sans-serif;max-width:760px;margin:32px auto}table{width:100%;border-collapse:collapse;margin-top:20px}th,td{border-bottom:1px solid #ddd;padding:8px;text-align:left}th:nth-child(n+2),td:nth-child(n+2){text-align:right}.total{text-align:right;margin-top:20px}</style></head><body>");
            html.AppendLine("<h1>BAKERY MANAGEMENT SYSTEM</h1><h2>HÓA ĐƠN BÁN HÀNG</h2>");
            html.AppendLine($"<p>Số hóa đơn: <strong>{WebUtility.HtmlEncode(order.Invoice.InvoiceNo)}</strong><br>Mã đơn: <strong>{WebUtility.HtmlEncode(order.OrderCode)}</strong><br>Ngày lập: {order.Invoice.IssuedAt:dd/MM/yyyy HH:mm}<br>Khách hàng: {WebUtility.HtmlEncode(order.Customer?.FullName ?? "Khách tại quầy")}<br>Người nhận: {WebUtility.HtmlEncode(order.Address?.ReceiverName ?? "Không có")}<br>Số điện thoại nhận hàng: {WebUtility.HtmlEncode(order.Address?.Phone ?? "Không có")}<br>Địa chỉ giao hàng: {WebUtility.HtmlEncode(order.Address?.AddressLine ?? "Nhận tại tiệm bánh")}<br>Phương thức thanh toán: {WebUtility.HtmlEncode(GetPaymentMethodText(order.PaymentMethod))}<br>Trạng thái thanh toán: {WebUtility.HtmlEncode(GetPaymentStatusText(order.PaymentStatus))}</p>");
            if (!string.IsNullOrWhiteSpace(order.DeliveryStaffName) || !string.IsNullOrWhiteSpace(order.DeliveryStaffPhone))
            {
                html.AppendLine($"<p><strong>Người giao hàng:</strong> {WebUtility.HtmlEncode(order.DeliveryStaffName ?? "Chưa cập nhật")}<br><strong>Số điện thoại:</strong> {WebUtility.HtmlEncode(order.DeliveryStaffPhone ?? "Chưa cập nhật")}</p>");
            }
            html.AppendLine("<table><thead><tr><th>Sản phẩm</th><th>Số lượng</th><th>Đơn giá</th><th>Thành tiền</th></tr></thead><tbody>");
            foreach (var item in order.OrderItems)
            {
                html.AppendLine($"<tr><td>{WebUtility.HtmlEncode(item.Product.Name)}</td><td>{item.Quantity}</td><td>{item.UnitPrice:N0} đ</td><td>{item.Quantity * item.UnitPrice:N0} đ</td></tr>");
            }
            html.AppendLine($"</tbody></table><div class=\"total\">Tạm tính: {order.Subtotal:N0} đ<br>Giảm giá: {order.DiscountAmount:N0} đ<h2>Tổng cộng: {order.TotalAmount:N0} đ</h2></div><p style=\"text-align:center\">Cảm ơn quý khách!</p></body></html>");

            return File(Encoding.UTF8.GetBytes(html.ToString()), "text/html", $"{order.Invoice.InvoiceNo}.html");
        }

        private static string GetPaymentMethodText(string paymentMethod) => paymentMethod switch
        {
            "COD" => "Thanh toán khi nhận hàng (COD)",
            "Cash" => "Tiền mặt",
            _ => paymentMethod
        };

        private static string GetPaymentStatusText(string paymentStatus) => paymentStatus switch
        {
            "Paid" => "Đã thanh toán",
            "Unpaid" => "Chưa thanh toán",
            "Refunded" => "Đã hoàn tiền",
            "Failed" => "Thanh toán thất bại",
            _ => paymentStatus
        };

        private static string GetOrderStatusText(string status) => status switch
        {
            "Pending" => "Chờ xử lý",
            "Preparing" => "Đang chuẩn bị",
            "Delivering" => "Đang giao hàng",
            "Completed" => "Đã hoàn thành",
            "Cancelled" => "Đã hủy",
            _ => status
        };

        private static bool IsValidStatusTransition(string current, string next) =>
            (current, next) switch
            {
                ("Pending", "Preparing") => true,
                ("Pending", "Cancelled") => true,
                ("Preparing", "Delivering") => true,
                ("Preparing", "Completed") => true,
                ("Preparing", "Cancelled") => true,
                ("Delivering", "Completed") => true,
                ("Delivering", "Cancelled") => true,
                _ => false
            };

        private async Task<CashierOrderDetailsViewModel?> BuildOrderDetailsAsync(int id)
        {
            // Truy vấn tổng hợp dữ liệu cho trang chi tiết:
            // Orders là bảng chính; Customer/Address/Invoice là quan hệ tham chiếu;
            // OrderItems chứa số lượng và giá tại thời điểm bán; Products chứa tên sản phẩm.
            var order = await _context.Orders.Include(o => o.Customer).Include(o => o.Address)
                .Include(o => o.Invoice).Include(o => o.OrderItems).ThenInclude(i => i.Product)
                .AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return null;
            return new CashierOrderDetailsViewModel
            {
                OrderId = order.OrderId, OrderCode = order.OrderCode, Channel = order.Channel,
                Status = order.Status, PaymentStatus = order.PaymentStatus, PaymentMethod = order.PaymentMethod,
                CustomerName = order.Customer?.FullName ?? "Khách vãng lai",
                ReceiverName = order.Address?.ReceiverName, DeliveryAddress = order.Address?.AddressLine,
                DeliveryStaffName = order.DeliveryStaffName, DeliveryStaffPhone = order.DeliveryStaffPhone,
                Subtotal = order.Subtotal, DiscountAmount = order.DiscountAmount, TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt, CompletedAt = order.CompletedAt, InvoiceNo = order.Invoice?.InvoiceNo,
                Items = order.OrderItems.Select(i => new CashierOrderItemViewModel { ProductName = i.Product.Name, Quantity = i.Quantity, UnitPrice = i.UnitPrice }).ToList()
            };
        }

        // =====================================================================
        // UC-09: Staff Profile
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
            if (user == null) return NotFound();

            return View(new CashierProfileViewModel
            {
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt,
                FullName = user.FullName,
                Phone = user.Phone
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(CashierProfileViewModel model)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
            if (user == null) return NotFound();

            // Các trường chỉ đọc luôn lấy lại từ DB, không tin dữ liệu từ form
            model.Email = user.Email;
            model.Role = user.Role;
            model.CreatedAt = user.CreatedAt;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            user.FullName = model.FullName.Trim();
            user.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
            await _context.SaveChangesAsync();

            // Làm mới claims trong cookie để tên/SĐT hiển thị đúng ngay lập tức
            await RefreshSignInAsync(user);

            _logger.LogInformation("Cashier {Email} đã cập nhật hồ sơ cá nhân.", user.Email);
            TempData["SuccessMessage"] = "Cập nhật thông tin cá nhân thành công.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
            if (user == null) return NotFound();

            if (!_passwordService.VerifyPassword(model.CurrentPassword, user.PasswordHash))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không chính xác.");
                return View(model);
            }

            if (model.CurrentPassword == model.NewPassword)
            {
                ModelState.AddModelError(nameof(model.NewPassword), "Mật khẩu mới phải khác mật khẩu hiện tại.");
                return View(model);
            }

            user.PasswordHash = _passwordService.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cashier {Email} đã đổi mật khẩu.", user.Email);
            TempData["SuccessMessage"] = "Đổi mật khẩu thành công.";
            return RedirectToAction(nameof(Profile));
        }

        // Ghi lại cookie với claims mới nhưng giữ nguyên thời hạn/Remember me cũ
        private async Task RefreshSignInAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("PhoneNumber", user.Phone ?? string.Empty)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var current = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                current.Properties ?? new AuthenticationProperties());
        }
    }
}