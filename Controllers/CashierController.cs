using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public CashierController(
            BakeryManagementDbContext context,
            IPasswordService passwordService,
            ILogger<CashierController> logger)
        {
            _context = context;
            _passwordService = passwordService;
            _logger = logger;
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