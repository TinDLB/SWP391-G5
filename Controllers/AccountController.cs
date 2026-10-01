using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;
using SWP391_G5.Services;

namespace SWP391_G5.Controllers
{
    public class AccountController : Controller
    {
        private readonly BakeryManagementDbContext _context;
        private readonly IPasswordService _passwordService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            BakeryManagementDbContext context,
            IPasswordService passwordService,
            ILogger<AccountController> logger)
        {
            _context = context;
            _passwordService = passwordService;
            _logger = logger;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            ViewData["ReturnUrl"] = model.ReturnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = model.Email.Trim().ToLower();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (user == null || !_passwordService.VerifyPassword(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản của bạn đã bị khóa hoặc tạm ngưng hoạt động. Vui lòng liên hệ tiệm bánh để được hỗ trợ.");
                return View(model);
            }

            // Tạo danh sách Claims xác thực
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("PhoneNumber", user.Phone ?? string.Empty)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(2)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            _logger.LogInformation("Người dùng {Email} (Role: {Role}) đã đăng nhập thành công.", user.Email, user.Role);

            TempData["SuccessMessage"] = $"Đăng nhập thành công! Chào mừng {user.FullName} trở lại tiệm bánh.";

            return RedirectToLocal(model.ReturnUrl, user.Role);
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new RegisterViewModel());
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = model.Email.Trim().ToLower();

            // Kiểm tra trùng email
            var emailExists = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

            if (emailExists)
            {
                ModelState.AddModelError("Email", "Địa chỉ email này đã được sử dụng. Vui lòng đăng nhập hoặc sử dụng email khác.");
                return View(model);
            }

            // Tạo người dùng mới với Role mặc định là Customer
            var newUser = new User
            {
                FullName = model.FullName.Trim(),
                Email = normalizedEmail,
                Phone = model.PhoneNumber.Trim(),
                PasswordHash = _passwordService.HashPassword(model.Password),
                Role = "Customer",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Đã tạo mới tài khoản Customer: {Email}", newUser.Email);

            // Tự động đăng nhập sau khi đăng ký thành công
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, newUser.UserId.ToString()),
                new Claim(ClaimTypes.Name, newUser.FullName),
                new Claim(ClaimTypes.Email, newUser.Email),
                new Claim(ClaimTypes.Role, newUser.Role),
                new Claim("PhoneNumber", newUser.Phone ?? string.Empty)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            TempData["SuccessMessage"] = $"Đăng ký thành công! Chào mừng {newUser.FullName} đến với Bakery Management System.";

            return RedirectToLocal(returnUrl);
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["InfoMessage"] = "Bạn đã đăng xuất tài khoản thành công.";
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToLocal(string? returnUrl, string? role = null)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) || User.IsInRole("Admin"))
            {
                return RedirectToAction("Dashboard", "Admin");
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
