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

            // Kiểm tra trùng email trong DB
            var emailExists = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

            if (emailExists)
            {
                ModelState.AddModelError("Email", "Địa chỉ email này đã được sử dụng. Vui lòng đăng nhập hoặc sử dụng email khác.");
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

            return RedirectToAction(nameof(VerifyRegisterOtp), new { email = normalizedEmail, returnUrl });
        }

        // GET: /Account/VerifyRegisterOtp
        [HttpGet]
        public IActionResult VerifyRegisterOtp(string email, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction(nameof(Register));
            }

            var normalizedEmail = email.Trim().ToLower();
            if (!_cache.TryGetValue($"PendingRegister_{normalizedEmail}", out PendingRegistration? pending))
            {
                TempData["ErrorMessage"] = "Phiên đăng ký hoặc mã OTP đã hết hạn (quá 5 phút). Vui lòng thực hiện đăng ký lại.";
                return RedirectToAction(nameof(Register));
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new VerifyOtpViewModel
            {
                Email = normalizedEmail,
                Purpose = "Register",
                ReturnUrl = returnUrl
            });
        }

        // POST: /Account/VerifyRegisterOtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyRegisterOtp(VerifyOtpViewModel model)
        {
            ViewData["ReturnUrl"] = model.ReturnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = model.Email.Trim().ToLower();
            if (!_cache.TryGetValue($"PendingRegister_{normalizedEmail}", out PendingRegistration? pending) || pending == null)
            {
                ModelState.AddModelError(string.Empty, "Phiên đăng ký của bạn đã hết hạn (quá 5 phút). Vui lòng thực hiện đăng ký lại.");
                return View(model);
            }

            // Kiểm tra mã OTP
            if (pending.OtpCode != model.OtpCode.Trim())
            {
                ModelState.AddModelError("OtpCode", "Mã OTP không chính xác. Vui lòng kiểm tra lại email.");
                return View(model);
            }

            // Kiểm tra lại xem email có bị đăng ký trong lúc chờ OTP không
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
            {
                ModelState.AddModelError(string.Empty, "Địa chỉ email này vừa được đăng ký bởi tài khoản khác.");
                return View(model);
            }

            // Tạo tài khoản chính thức vào SQL Server
            var newUser = new User
            {
                FullName = pending.FullName,
                Email = pending.Email,
                Phone = pending.PhoneNumber,
                PasswordHash = _passwordService.HashPassword(pending.Password),
                Role = Role.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Lưu địa chỉ giao hàng nếu có
            if (!string.IsNullOrWhiteSpace(pending.Address))
            {
                var newAddress = new Address
                {
                    UserId = newUser.UserId,
                    ReceiverName = newUser.FullName,
                    Phone = newUser.Phone ?? string.Empty,
                    AddressLine = pending.Address,
                    IsDefault = true
                };
                _context.Addresses.Add(newAddress);
                await _context.SaveChangesAsync();
            }

            // Xóa cache đăng ký tạm
            _cache.Remove($"PendingRegister_{normalizedEmail}");

            _logger.LogInformation("Người dùng {Email} đã xác thực OTP đăng ký thành công.", newUser.Email);

            TempData["SuccessMessage"] = $"Xác thực tài khoản thành công! Bạn có thể sử dụng email {newUser.Email} và mật khẩu vừa tạo để đăng nhập.";

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
            if (_cache.TryGetValue($"PendingRegister_{normalizedEmail}", out PendingRegistration? pending) && pending != null)
            {
                var newOtp = Random.Shared.Next(100000, 999999).ToString();
                pending.OtpCode = newOtp;
                _cache.Set($"PendingRegister_{normalizedEmail}", pending, TimeSpan.FromMinutes(5));

                await _emailService.SendOtpEmailAsync(normalizedEmail, newOtp, "Register");
                TempData["SuccessMessage"] = "Mã OTP mới đã được gửi lại vào email của bạn.";
            }
            else
            {
                TempData["ErrorMessage"] = "Phiên đăng ký đã hết hạn. Vui lòng đăng ký lại.";
                return RedirectToAction(nameof(Register));
            }

            return RedirectToAction(nameof(VerifyRegisterOtp), new { email = normalizedEmail, returnUrl });
        }

        #endregion

        #region --- 3. QUÊN MẬT KHẨU & ĐẶT LẠI MẬT KHẨU QUA OTP ---

        // GET: /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
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
                ModelState.AddModelError("Email", "Địa chỉ email này chưa được đăng ký trong hệ thống.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ ban quản trị tiệm bánh.");
                return View(model);
            }

            // Sinh mã OTP 6 chữ số
            var otpCode = Random.Shared.Next(100000, 999999).ToString();
            _cache.Set($"ForgotPassOtp_{normalizedEmail}", otpCode, TimeSpan.FromMinutes(5));

            await _emailService.SendOtpEmailAsync(normalizedEmail, otpCode, "ForgotPassword");

            TempData["InfoMessage"] = $"Mã OTP khôi phục mật khẩu đã được gửi đến {normalizedEmail}. Vui lòng kiểm tra email.";

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

            var normalizedEmail = email.Trim().ToLower();
            if (!_cache.TryGetValue($"ForgotPassOtp_{normalizedEmail}", out string? _))
            {
                TempData["ErrorMessage"] = "Mã OTP đã hết hạn. Vui lòng yêu cầu gửi lại mã mới.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new VerifyOtpViewModel
            {
                Email = normalizedEmail,
                Purpose = "ForgotPassword"
            });
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
            if (!_cache.TryGetValue($"ForgotPassOtp_{normalizedEmail}", out string? cachedOtp) || string.IsNullOrEmpty(cachedOtp))
            {
                ModelState.AddModelError(string.Empty, "Mã OTP đã hết hạn (quá 5 phút). Vui lòng yêu cầu gửi lại.");
                return View(model);
            }

            if (cachedOtp != model.OtpCode.Trim())
            {
                ModelState.AddModelError("OtpCode", "Mã OTP không chính xác. Vui lòng thử lại.");
                return View(model);
            }

            // OTP đúng -> Sinh Reset Token an toàn có hạn trong 10 phút
            var resetToken = Guid.NewGuid().ToString("N");
            _cache.Set($"ResetPasswordToken_{normalizedEmail}", resetToken, TimeSpan.FromMinutes(10));
            _cache.Remove($"ForgotPassOtp_{normalizedEmail}");

            return RedirectToAction(nameof(ResetPassword), new { email = normalizedEmail, token = resetToken });
        }

        // POST: /Account/ResendForgotOtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendForgotOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            var normalizedEmail = email.Trim().ToLower();
            var newOtp = Random.Shared.Next(100000, 999999).ToString();
            _cache.Set($"ForgotPassOtp_{normalizedEmail}", newOtp, TimeSpan.FromMinutes(5));

            await _emailService.SendOtpEmailAsync(normalizedEmail, newOtp, "ForgotPassword");
            TempData["SuccessMessage"] = "Mã OTP mới đã được gửi lại vào email của bạn.";

            return RedirectToAction(nameof(VerifyForgotOtp), new { email = normalizedEmail });
        }

        // GET: /Account/ResetPassword
        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                TempData["ErrorMessage"] = "Yêu cầu đặt lại mật khẩu không hợp lệ.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var normalizedEmail = email.Trim().ToLower();
            if (!_cache.TryGetValue($"ResetPasswordToken_{normalizedEmail}", out string? cachedToken) || cachedToken != token)
            {
                TempData["ErrorMessage"] = "Phiên đặt lại mật khẩu đã hết hạn hoặc không hợp lệ. Vui lòng thử lại.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new ResetPasswordViewModel
            {
                Email = normalizedEmail,
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
            if (!_cache.TryGetValue($"ResetPasswordToken_{normalizedEmail}", out string? cachedToken) || cachedToken != model.Token)
            {
                ModelState.AddModelError(string.Empty, "Phiên đặt lại mật khẩu đã hết hạn hoặc không hợp lệ. Vui lòng thực hiện lại từ đầu.");
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy tài khoản người dùng.");
                return View(model);
            }

            // Cập nhật mật khẩu mới đã băm BCrypt
            user.PasswordHash = _passwordService.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            // Xóa token
            _cache.Remove($"ResetPasswordToken_{normalizedEmail}");

            _logger.LogInformation("Người dùng {Email} đã đặt lại mật khẩu thành công qua OTP.", user.Email);

            TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công! Bạn có thể sử dụng mật khẩu mới để đăng nhập ngay bây giờ.";

            return RedirectToAction(nameof(Login), new { email = user.Email });
        }

        #endregion

        #region --- 4. QUẢN LÝ TRANG TÀI KHOẢN (PROFILE & CHANGE PASSWORD) ---

        // GET: /Account/Profile
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
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

            var defaultAddress = user.Addresses.FirstOrDefault(a => a.IsDefault)?.AddressLine
                ?? user.Addresses.FirstOrDefault()?.AddressLine;

            var model = new ProfileViewModel
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.Phone ?? string.Empty,
                Address = defaultAddress,
                Role = user.Role
            };

            return View(model);
        }

        // POST: /Account/Profile
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

            if (!ModelState.IsValid)
            {
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

            // Cập nhật hoặc thêm mới địa chỉ nhận hàng mặc định
            if (!string.IsNullOrWhiteSpace(model.Address))
            {
                var existingAddress = user.Addresses.FirstOrDefault(a => a.IsDefault)
                    ?? user.Addresses.FirstOrDefault();

                if (existingAddress != null)
                {
                    existingAddress.AddressLine = model.Address.Trim();
                    existingAddress.ReceiverName = user.FullName;
                    existingAddress.Phone = user.Phone;
                    existingAddress.IsDefault = true;
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

            // Cập nhật lại Claims trong Cookie để tên mới hiển thị ngay lập tức trên Navbar
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
            return RedirectToAction(nameof(Profile));
        }

        // GET: /Account/ChangePassword
        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        // POST: /Account/ChangePassword
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                return RedirectToAction(nameof(Login));
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            // Kiểm tra mật khẩu hiện tại
            if (!_passwordService.VerifyPassword(model.CurrentPassword, user.PasswordHash))
            {
                ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không chính xác.");
                return View(model);
            }

            // Cập nhật mật khẩu mới
            user.PasswordHash = _passwordService.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Người dùng {Email} đã đổi mật khẩu thành công trong trang cá nhân.", user.Email);

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToAction(nameof(Profile));
        }

        #endregion

        #region --- HELPER METHODS ---

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
