using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
        private readonly IEmailService _emailService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            BakeryManagementDbContext context,
            IPasswordService passwordService,
            IEmailService emailService,
            IMemoryCache cache,
            ILogger<AccountController> logger)
        {
            _context = context;
            _passwordService = passwordService;
            _emailService = emailService;
            _cache = cache;
            _logger = logger;
        }

        #region --- 1. ĐĂNG NHẬP (LOGIN) ---

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? email = null, string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel
            {
                Email = email ?? string.Empty,
                ReturnUrl = returnUrl
            });
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

            // Đồng bộ giỏ hàng từ Guest Session vào tài khoản của khách (UC-04)
            await MergeSessionCartAsync(user.UserId);

            _logger.LogInformation("Người dùng {Email} (Role: {Role}) đã đăng nhập thành công.", user.Email, user.Role);

            TempData["SuccessMessage"] = $"Đăng nhập thành công! Chào mừng {user.FullName} trở lại tiệm bánh.";

            return RedirectToLocal(model.ReturnUrl, user.Role);
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

        #endregion

        #region --- 2. ĐĂNG KÝ VỚI XÁC THỰC OTP QUA EMAIL ---

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new RegisterViewModel { ReturnUrl = returnUrl });
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            ViewData["ReturnUrl"] = model.ReturnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = model.Email.Trim().ToLower();

            // Kiểm tra trùng email trong DB
            var emailExists = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

            if (emailExists)
            {
                ModelState.AddModelError("Email", "Email này đã được sử dụng. Vui lòng chọn email khác hoặc Đăng nhập.");
                return View(model);
            }

            // Sinh mã OTP 6 chữ số ngẫu nhiên
            var otpCode = Random.Shared.Next(100000, 999999).ToString();

            // Lưu dữ liệu đăng ký tạm vào MemoryCache trong 5 phút
            var pending = new PendingRegistration
            {
                FullName = model.FullName.Trim(),
                Email = normalizedEmail,
                PhoneNumber = model.PhoneNumber.Trim(),
                Password = model.Password,
                Address = model.Address?.Trim(),
                OtpCode = otpCode,
                CreatedAt = DateTime.UtcNow
            };

            _cache.Set($"PendingRegister_{normalizedEmail}", pending, TimeSpan.FromMinutes(5));

            // Gửi email OTP
            await _emailService.SendOtpEmailAsync(normalizedEmail, otpCode, "Register");

            TempData["InfoMessage"] = $"Mã OTP xác thực đã được gửi đến email {normalizedEmail}. Vui lòng nhập mã để hoàn tất đăng ký.";

            return RedirectToAction(nameof(VerifyRegisterOtp), new { email = normalizedEmail, returnUrl = model.ReturnUrl });
        }

        // GET: /Account/VerifyRegisterOtp
        [HttpGet]
        public IActionResult VerifyRegisterOtp(string email, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction(nameof(Register));
            }

            return View(new VerifyOtpViewModel
            {
                Email = email,
                ReturnUrl = returnUrl
            });
        }

        // POST: /Account/VerifyRegisterOtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyRegisterOtp(VerifyOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = model.Email.Trim().ToLower();

            if (!_cache.TryGetValue<PendingRegistration>($"PendingRegister_{normalizedEmail}", out var pending) || pending == null)
            {
                ModelState.AddModelError(string.Empty, "Mã OTP đã hết hạn hoặc không tồn tại. Vui lòng yêu cầu gửi lại mã mới.");
                return View(model);
            }

            if (!string.Equals(pending.OtpCode, model.OtpCode.Trim(), StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(model.OtpCode), "Mã OTP không chính xác. Vui lòng kiểm tra lại.");
                return View(model);
            }

            // Kiểm tra lại trùng email lần cuối
            var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
            if (emailExists)
            {
                _cache.Remove($"PendingRegister_{normalizedEmail}");
                ModelState.AddModelError(string.Empty, "Email này đã được đăng ký trong hệ thống.");
                return View(model);
            }

            // Tạo người dùng mới
            var newUser = new User
            {
                FullName = pending.FullName,
                Email = pending.Email,
                Phone = pending.PhoneNumber,
                PasswordHash = _passwordService.HashPassword(pending.Password),
                Role = "Customer",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LoyaltyPoints = 0
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Nếu có địa chỉ thì lưu vào bảng Addresses
            if (!string.IsNullOrWhiteSpace(pending.Address))
            {
                _context.Addresses.Add(new Address
                {
                    UserId = newUser.UserId,
                    ReceiverName = newUser.FullName,
                    Phone = newUser.Phone ?? "",
                    AddressLine = pending.Address.Trim(),
                    IsDefault = true
                });
                await _context.SaveChangesAsync();
            }

            // Xóa cache đăng ký
            _cache.Remove($"PendingRegister_{normalizedEmail}");

            _logger.LogInformation("Người dùng mới {Email} đã hoàn tất đăng ký tài khoản qua xác thực OTP thành công.", newUser.Email);

            TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Bạn có thể đăng nhập ngay bây giờ.";

            return RedirectToAction(nameof(Login), new { email = newUser.Email, returnUrl = model.ReturnUrl });
        }

        // POST: /Account/ResendRegisterOtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendRegisterOtp(string email, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction(nameof(Register));
            }

            var normalizedEmail = email.Trim().ToLower();

            if (!_cache.TryGetValue<PendingRegistration>($"PendingRegister_{normalizedEmail}", out var pending) || pending == null)
            {
                TempData["ErrorMessage"] = "Phiên đăng ký đã hết hạn. Vui lòng điền lại thông tin đăng ký.";
                return RedirectToAction(nameof(Register));
            }

            var newOtp = Random.Shared.Next(100000, 999999).ToString();
            pending.OtpCode = newOtp;
            pending.CreatedAt = DateTime.UtcNow;

            _cache.Set($"PendingRegister_{normalizedEmail}", pending, TimeSpan.FromMinutes(5));

            await _emailService.SendOtpEmailAsync(normalizedEmail, newOtp, "Register");

            TempData["SuccessMessage"] = "Mã OTP mới đã được gửi đến email của bạn. Vui lòng kiểm tra hộp thư.";

            return RedirectToAction(nameof(VerifyRegisterOtp), new { email = normalizedEmail, returnUrl });
        }

        #endregion

        #region --- 3. QUÊN MẬT KHẨU VỚI XÁC THỰC OTP ---

        // GET: /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = model.Email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

            if (user == null)
            {
                ModelState.AddModelError("Email", "Không tìm thấy tài khoản nào liên kết với email này.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.");
                return View(model);
            }

            var otpCode = Random.Shared.Next(100000, 999999).ToString();
            _cache.Set($"ForgotPassOtp_{normalizedEmail}", otpCode, TimeSpan.FromMinutes(5));

            await _emailService.SendOtpEmailAsync(normalizedEmail, otpCode, "ForgotPassword");

            TempData["InfoMessage"] = $"Mã OTP khôi phục mật khẩu đã được gửi đến {normalizedEmail}. Vui lòng nhập mã để tiếp tục.";

            return RedirectToAction(nameof(VerifyForgotOtp), new { email = normalizedEmail });
        }

        // GET: /Account/VerifyForgotOtp
        [HttpGet]
        public IActionResult VerifyForgotOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new VerifyOtpViewModel { Email = email });
        }

        // POST: /Account/VerifyForgotOtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyForgotOtp(VerifyOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = model.Email.Trim().ToLower();

            if (!_cache.TryGetValue<string>($"ForgotPassOtp_{normalizedEmail}", out var cachedOtp) || cachedOtp == null)
            {
                ModelState.AddModelError(string.Empty, "Mã OTP đã hết hạn. Vui lòng yêu cầu gửi lại mã mới.");
                return View(model);
            }

            if (!string.Equals(cachedOtp, model.OtpCode.Trim(), StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(model.OtpCode), "Mã OTP không chính xác. Vui lòng kiểm tra lại.");
                return View(model);
            }

            _cache.Remove($"ForgotPassOtp_{normalizedEmail}");

            var resetToken = Guid.NewGuid().ToString("N");
            _cache.Set($"ResetPasswordToken_{normalizedEmail}", resetToken, TimeSpan.FromMinutes(15));

            return RedirectToAction(nameof(ResetPassword), new { email = normalizedEmail, token = resetToken });
        }

        // GET: /Account/ResetPassword
        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            var normalizedEmail = email.Trim().ToLower();
            if (!_cache.TryGetValue<string>($"ResetPasswordToken_{normalizedEmail}", out var cachedToken) || cachedToken != token)
            {
                TempData["ErrorMessage"] = "Yêu cầu đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng thử lại.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new ResetPasswordViewModel
            {
                Email = email,
                Token = token
            });
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = model.Email.Trim().ToLower();
            if (!_cache.TryGetValue<string>($"ResetPasswordToken_{normalizedEmail}", out var cachedToken) || cachedToken != model.Token)
            {
                TempData["ErrorMessage"] = "Phiên đặt lại mật khẩu đã hết hạn. Vui lòng thực hiện lại từ đầu.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            user.PasswordHash = _passwordService.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            _cache.Remove($"ResetPasswordToken_{normalizedEmail}");

            _logger.LogInformation("Người dùng {Email} đã đặt lại mật khẩu thành công qua OTP.", user.Email);

            TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công! Bạn có thể sử dụng mật khẩu mới để đăng nhập ngay bây giờ.";

            return RedirectToAction(nameof(Login), new { email = user.Email });
        }

        #endregion

        #region --- 4. QUẢN LÝ TRANG TÀI KHOẢN & SỔ ĐỊA CHỈ (UC-05: PROFILE & ADDRESSES) ---

        // GET: /Account/Profile
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile(string? tab = null)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Dashboard", "Admin");
            if (string.Equals(role, "Cashier", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Dashboard", "Cashier");
            if (string.Equals(role, "Baker", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Baker");

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                return RedirectToAction(nameof(Login));
            }

            var user = await _context.Users
                .Include(u => u.Addresses)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var savedAddresses = user.Addresses
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.AddressId)
                .ToList();

            var defaultAddress = savedAddresses.FirstOrDefault(a => a.IsDefault)?.AddressLine
                ?? savedAddresses.FirstOrDefault()?.AddressLine;

            var activeTab = !string.IsNullOrEmpty(tab)
                ? tab
                : (TempData["ActiveTab"]?.ToString() ?? "info");

            var model = new ProfileViewModel
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.Phone ?? string.Empty,
                Address = defaultAddress,
                Role = user.Role,
                LoyaltyPoints = user.LoyaltyPoints,
                Addresses = savedAddresses,
                ActiveTab = activeTab
            };

            return View(model);
        }

        // POST: /Account/Profile (Cập nhật thông tin cá nhân)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId) || userId != model.UserId)
            {
                return RedirectToAction(nameof(Login));
            }

            ModelState.Remove("ChangePassword");
            ModelState.Remove("ChangePassword.CurrentPassword");
            ModelState.Remove("ChangePassword.NewPassword");
            ModelState.Remove("ChangePassword.ConfirmNewPassword");
            ModelState.Remove("NewAddress");
            ModelState.Remove("NewAddress.ReceiverName");
            ModelState.Remove("NewAddress.Phone");
            ModelState.Remove("NewAddress.AddressLine");

            if (!ModelState.IsValid)
            {
                var userReload = await _context.Users.Include(u => u.Addresses).FirstOrDefaultAsync(u => u.UserId == userId);
                model.Addresses = userReload?.Addresses.OrderByDescending(a => a.IsDefault).ToList() ?? new List<Address>();
                model.ActiveTab = "info";
                return View(model);
            }

            var user = await _context.Users
                .Include(u => u.Addresses)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            user.FullName = model.FullName.Trim();
            user.Phone = model.PhoneNumber.Trim();

            // Nếu người dùng nhập địa chỉ và chưa có địa chỉ mặc định, lưu vào Addresses
            if (!string.IsNullOrWhiteSpace(model.Address))
            {
                var defaultAddr = user.Addresses.FirstOrDefault(a => a.IsDefault);
                if (defaultAddr != null)
                {
                    defaultAddr.AddressLine = model.Address.Trim();
                }
                else
                {
                    _context.Addresses.Add(new Address
                    {
                        UserId = user.UserId,
                        ReceiverName = user.FullName,
                        Phone = user.Phone,
                        AddressLine = model.Address.Trim(),
                        IsDefault = true
                    });
                }
            }

            await _context.SaveChangesAsync();

            // Cập nhật Claims Cookie
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("PhoneNumber", user.Phone ?? string.Empty)
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            TempData["SuccessMessage"] = "Cập nhật thông tin tài khoản thành công!";
            TempData["ActiveTab"] = "info";
            return RedirectToAction(nameof(Profile), new { tab = "info" });
        }

        // POST: /Account/ChangePassword
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                return RedirectToAction(nameof(Login));
            }

            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault() ?? "Dữ liệu đổi mật khẩu không hợp lệ.";

                TempData["ErrorMessage"] = firstError;
                TempData["ActiveTab"] = "password";
                return RedirectToAction(nameof(Profile), new { tab = "password" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            if (!_passwordService.VerifyPassword(model.CurrentPassword, user.PasswordHash))
            {
                TempData["ErrorMessage"] = "Mật khẩu hiện tại không chính xác. Vui lòng kiểm tra lại.";
                TempData["ActiveTab"] = "password";
                return RedirectToAction(nameof(Profile), new { tab = "password" });
            }

            user.PasswordHash = _passwordService.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Người dùng {Email} đã đổi mật khẩu thành công.", user.Email);

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            TempData["ActiveTab"] = "password";
            return RedirectToAction(nameof(Profile), new { tab = "password" });
        }

        // POST: /Account/AddAddress (Thêm địa chỉ giao hàng mới)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAddress(AddressInputModel model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                return RedirectToAction(nameof(Login));
            }

            if (string.IsNullOrWhiteSpace(model.ReceiverName) || string.IsNullOrWhiteSpace(model.Phone) || string.IsNullOrWhiteSpace(model.AddressLine))
            {
                TempData["ErrorMessage"] = "Vui lòng điền đầy đủ họ tên, số điện thoại và địa chỉ nhận hàng.";
                return RedirectToAction(nameof(Profile), new { tab = "address" });
            }

            var existingAddresses = await _context.Addresses.Where(a => a.UserId == userId).ToListAsync();
            bool isFirst = !existingAddresses.Any();

            if (model.IsDefault || isFirst)
            {
                foreach (var a in existingAddresses)
                {
                    a.IsDefault = false;
                }
            }

            var newAddress = new Address
            {
                UserId = userId,
                ReceiverName = model.ReceiverName.Trim(),
                Phone = model.Phone.Trim(),
                AddressLine = model.AddressLine.Trim(),
                IsDefault = model.IsDefault || isFirst
            };

            _context.Addresses.Add(newAddress);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm địa chỉ nhận hàng mới thành công.";
            return RedirectToAction(nameof(Profile), new { tab = "address" });
        }

        // POST: /Account/UpdateAddress (Sửa địa chỉ giao hàng)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAddress(AddressInputModel model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                return RedirectToAction(nameof(Login));
            }

            var address = await _context.Addresses.FirstOrDefaultAsync(a => a.AddressId == model.AddressId && a.UserId == userId);
            if (address == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy địa chỉ cần cập nhật.";
                return RedirectToAction(nameof(Profile), new { tab = "address" });
            }

            if (model.IsDefault && !address.IsDefault)
            {
                var userAddresses = await _context.Addresses.Where(a => a.UserId == userId).ToListAsync();
                foreach (var a in userAddresses)
                {
                    a.IsDefault = false;
                }
            }

            address.ReceiverName = model.ReceiverName.Trim();
            address.Phone = model.Phone.Trim();
            address.AddressLine = model.AddressLine.Trim();
            if (model.IsDefault) address.IsDefault = true;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật địa chỉ nhận hàng thành công.";
            return RedirectToAction(nameof(Profile), new { tab = "address" });
        }

        // POST: /Account/DeleteAddress (Xóa địa chỉ)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAddress(int addressId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                return RedirectToAction(nameof(Login));
            }

            var address = await _context.Addresses.FirstOrDefaultAsync(a => a.AddressId == addressId && a.UserId == userId);
            if (address == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy địa chỉ cần xóa.";
                return RedirectToAction(nameof(Profile), new { tab = "address" });
            }

            // Kiểm tra ràng buộc đơn hàng
            bool isUsedInOrders = await _context.Orders.AnyAsync(o => o.AddressId == addressId);
            if (isUsedInOrders)
            {
                TempData["ErrorMessage"] = "Địa chỉ này đã được sử dụng trong các đơn hàng đã đặt của bạn, không thể xóa để lưu trữ lịch sử vận chuyển.";
                return RedirectToAction(nameof(Profile), new { tab = "address" });
            }

            bool wasDefault = address.IsDefault;
            _context.Addresses.Remove(address);
            await _context.SaveChangesAsync();

            // Nếu xóa địa chỉ mặc định, gán địa chỉ đầu tiên còn lại làm mặc định
            if (wasDefault)
            {
                var remaining = await _context.Addresses.FirstOrDefaultAsync(a => a.UserId == userId);
                if (remaining != null)
                {
                    remaining.IsDefault = true;
                    await _context.SaveChangesAsync();
                }
            }

            TempData["SuccessMessage"] = "Đã xóa địa chỉ thành công.";
            return RedirectToAction(nameof(Profile), new { tab = "address" });
        }

        // POST: /Account/SetDefaultAddress (Đặt làm mặc định)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefaultAddress(int addressId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                return RedirectToAction(nameof(Login));
            }

            var addresses = await _context.Addresses.Where(a => a.UserId == userId).ToListAsync();
            var target = addresses.FirstOrDefault(a => a.AddressId == addressId);
            if (target == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy địa chỉ.";
                return RedirectToAction(nameof(Profile), new { tab = "address" });
            }

            foreach (var a in addresses)
            {
                a.IsDefault = (a.AddressId == addressId);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã cập nhật địa chỉ mặc định.";
            return RedirectToAction(nameof(Profile), new { tab = "address" });
        }

        #endregion

        #region --- HELPER METHODS ---

        private async Task MergeSessionCartAsync(int userId)
        {
            var json = HttpContext.Session.GetString(CartController.CartSessionKey);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var sessionCart = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(json);
                    if (sessionCart != null && sessionCart.Any())
                    {
                        foreach (var kvp in sessionCart)
                        {
                            var existing = await _context.CartItems.FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == kvp.Key);
                            if (existing != null)
                            {
                                existing.Quantity += kvp.Value;
                            }
                            else
                            {
                                _context.CartItems.Add(new CartItem
                                {
                                    UserId = userId,
                                    ProductId = kvp.Key,
                                    Quantity = kvp.Value
                                });
                            }
                        }
                        await _context.SaveChangesAsync();
                        HttpContext.Session.Remove(CartController.CartSessionKey);
                    }
                }
                catch
                {
                    // Ignore session parsing errors
                }
            }
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
            if (string.Equals(role, "Cashier", StringComparison.OrdinalIgnoreCase) || User.IsInRole("Cashier"))
            {
                return RedirectToAction("Dashboard", "Cashier");
            }
            if (string.Equals(role, "Baker", StringComparison.OrdinalIgnoreCase) || User.IsInRole("Baker"))
            {
                return RedirectToAction("Index", "Baker");
            }

            return RedirectToAction("Index", "Home");
        }

        #endregion
    }
}
