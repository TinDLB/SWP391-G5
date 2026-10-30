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

        #region --- 1. ÄÄ‚NG NHáº¬P (LOGIN) ---

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
                ModelState.AddModelError(string.Empty, "Email hoáº·c máº­t kháº©u khÃ´ng chÃ­nh xÃ¡c.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "TÃ i khoáº£n cá»§a báº¡n Ä‘Ã£ bá»‹ khÃ³a hoáº·c táº¡m ngÆ°ng hoáº¡t Ä‘á»™ng. Vui lÃ²ng liÃªn há»‡ tiá»‡m bÃ¡nh Ä‘á»ƒ Ä‘Æ°á»£c há»— trá»£.");
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

            // Äá»“ng bá»™ giá» hÃ ng tá»« Guest Session vÃ o tÃ i khoáº£n cá»§a khÃ¡ch (UC-04)
            await MergeSessionCartAsync(user.UserId);

            _logger.LogInformation("NgÆ°á»i dÃ¹ng {Email} (Role: {Role}) Ä‘Ã£ Ä‘Äƒng nháº­p thÃ nh cÃ´ng.", user.Email, user.Role);

            TempData["SuccessMessage"] = $"ÄÄƒng nháº­p thÃ nh cÃ´ng! ChÃ o má»«ng {user.FullName} trá»Ÿ láº¡i tiá»‡m bÃ¡nh.";

            return RedirectToLocal(model.ReturnUrl, user.Role);
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["InfoMessage"] = "Báº¡n Ä‘Ã£ Ä‘Äƒng xuáº¥t tÃ i khoáº£n thÃ nh cÃ´ng.";
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        #endregion

        #region --- 2. ÄÄ‚NG KÃ Vá»šI XÃC THá»°C OTP QUA EMAIL ---

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

            // Kiá»ƒm tra trÃ¹ng email trong DB
            var emailExists = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

            if (emailExists)
            {
                ModelState.AddModelError("Email", "Email nÃ y Ä‘Ã£ Ä‘Æ°á»£c sá»­ dá»¥ng. Vui lÃ²ng chá»n email khÃ¡c hoáº·c ÄÄƒng nháº­p.");
                return View(model);
            }

            // Sinh mÃ£ OTP 6 chá»¯ sá»‘ ngáº«u nhiÃªn
            var otpCode = Random.Shared.Next(100000, 999999).ToString();

            // LÆ°u dá»¯ liá»‡u Ä‘Äƒng kÃ½ táº¡m vÃ o MemoryCache trong 5 phÃºt
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

            // Gá»­i email OTP
            await _emailService.SendOtpEmailAsync(normalizedEmail, otpCode, "Register");

            TempData["InfoMessage"] = $"MÃ£ OTP xÃ¡c thá»±c Ä‘Ã£ Ä‘Æ°á»£c gá»­i Ä‘áº¿n email {normalizedEmail}. Vui lÃ²ng nháº­p mÃ£ Ä‘á»ƒ hoÃ n táº¥t Ä‘Äƒng kÃ½.";

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

            var normalizedEmail = email.Trim().ToLower();
            if (_cache.TryGetValue<PendingRegistration>($"PendingRegister_{normalizedEmail}", out var pending) && pending != null)
            {
                ViewBag.DevOtp = pending.OtpCode;
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
                ModelState.AddModelError(string.Empty, "MÃ£ OTP Ä‘Ã£ háº¿t háº¡n hoáº·c khÃ´ng tá»“n táº¡i. Vui lÃ²ng yÃªu cáº§u gá»­i láº¡i mÃ£ má»›i.");
                return View(model);
            }

            if (!string.Equals(pending.OtpCode, model.OtpCode.Trim(), StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(model.OtpCode), "MÃ£ OTP khÃ´ng chÃ­nh xÃ¡c. Vui lÃ²ng kiá»ƒm tra láº¡i.");
                return View(model);
            }

            // Kiá»ƒm tra láº¡i trÃ¹ng email láº§n cuá»‘i
            var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
            if (emailExists)
            {
                _cache.Remove($"PendingRegister_{normalizedEmail}");
                ModelState.AddModelError(string.Empty, "Email nÃ y Ä‘Ã£ Ä‘Æ°á»£c Ä‘Äƒng kÃ½ trong há»‡ thá»‘ng.");
                return View(model);
            }

            // Táº¡o ngÆ°á»i dÃ¹ng má»›i
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

            // Náº¿u cÃ³ Ä‘á»‹a chá»‰ thÃ¬ lÆ°u vÃ o báº£ng Addresses
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

            // XÃ³a cache Ä‘Äƒng kÃ½
            _cache.Remove($"PendingRegister_{normalizedEmail}");

            _logger.LogInformation("NgÆ°á»i dÃ¹ng má»›i {Email} Ä‘Ã£ hoÃ n táº¥t Ä‘Äƒng kÃ½ tÃ i khoáº£n qua xÃ¡c thá»±c OTP thÃ nh cÃ´ng.", newUser.Email);

            TempData["SuccessMessage"] = "ÄÄƒng kÃ½ tÃ i khoáº£n thÃ nh cÃ´ng! Báº¡n cÃ³ thá»ƒ Ä‘Äƒng nháº­p ngay bÃ¢y giá».";

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
                TempData["ErrorMessage"] = "PhiÃªn Ä‘Äƒng kÃ½ Ä‘Ã£ háº¿t háº¡n. Vui lÃ²ng Ä‘iá»n láº¡i thÃ´ng tin Ä‘Äƒng kÃ½.";
                return RedirectToAction(nameof(Register));
            }

            var newOtp = Random.Shared.Next(100000, 999999).ToString();
            pending.OtpCode = newOtp;
            pending.CreatedAt = DateTime.UtcNow;

            _cache.Set($"PendingRegister_{normalizedEmail}", pending, TimeSpan.FromMinutes(5));

            await _emailService.SendOtpEmailAsync(normalizedEmail, newOtp, "Register");

            TempData["SuccessMessage"] = "MÃ£ OTP má»›i Ä‘Ã£ Ä‘Æ°á»£c gá»­i Ä‘áº¿n email cá»§a báº¡n. Vui lÃ²ng kiá»ƒm tra há»™p thÆ°.";

            return RedirectToAction(nameof(VerifyRegisterOtp), new { email = normalizedEmail, returnUrl });
        }

        #endregion

        #region --- 3. QUÃŠN Máº¬T KHáº¨U Vá»šI XÃC THá»°C OTP ---

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
                ModelState.AddModelError("Email", "KhÃ´ng tÃ¬m tháº¥y tÃ i khoáº£n nÃ o liÃªn káº¿t vá»›i email nÃ y.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "TÃ i khoáº£n cá»§a báº¡n Ä‘Ã£ bá»‹ khÃ³a. Vui lÃ²ng liÃªn há»‡ quáº£n trá»‹ viÃªn.");
                return View(model);
            }

            var otpCode = Random.Shared.Next(100000, 999999).ToString();
            _cache.Set($"ForgotPassOtp_{normalizedEmail}", otpCode, TimeSpan.FromMinutes(5));

            await _emailService.SendOtpEmailAsync(normalizedEmail, otpCode, "ForgotPassword");

            TempData["InfoMessage"] = $"MÃ£ OTP khÃ´i phá»¥c máº­t kháº©u Ä‘Ã£ Ä‘Æ°á»£c gá»­i Ä‘áº¿n {normalizedEmail}. Vui lÃ²ng nháº­p mÃ£ Ä‘á»ƒ tiáº¿p tá»¥c.";

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
            if (_cache.TryGetValue<string>($"ForgotPassOtp_{normalizedEmail}", out var cachedOtp) && !string.IsNullOrEmpty(cachedOtp))
            {
                ViewBag.DevOtp = cachedOtp;
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
                ModelState.AddModelError(string.Empty, "MÃ£ OTP Ä‘Ã£ háº¿t háº¡n. Vui lÃ²ng yÃªu cáº§u gá»­i láº¡i mÃ£ má»›i.");
                return View(model);
            }

            if (!string.Equals(cachedOtp, model.OtpCode.Trim(), StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(model.OtpCode), "MÃ£ OTP khÃ´ng chÃ­nh xÃ¡c. Vui lÃ²ng kiá»ƒm tra láº¡i.");
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
                TempData["ErrorMessage"] = "YÃªu cáº§u Ä‘áº·t láº¡i máº­t kháº©u khÃ´ng há»£p lá»‡ hoáº·c Ä‘Ã£ háº¿t háº¡n. Vui lÃ²ng thá»­ láº¡i.";
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
                TempData["ErrorMessage"] = "PhiÃªn Ä‘áº·t láº¡i máº­t kháº©u Ä‘Ã£ háº¿t háº¡n. Vui lÃ²ng thá»±c hiá»‡n láº¡i tá»« Ä‘áº§u.";
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

            _logger.LogInformation("NgÆ°á»i dÃ¹ng {Email} Ä‘Ã£ Ä‘áº·t láº¡i máº­t kháº©u thÃ nh cÃ´ng qua OTP.", user.Email);

            TempData["SuccessMessage"] = "Äáº·t láº¡i máº­t kháº©u thÃ nh cÃ´ng! Báº¡n cÃ³ thá»ƒ sá»­ dá»¥ng máº­t kháº©u má»›i Ä‘á»ƒ Ä‘Äƒng nháº­p ngay bÃ¢y giá».";

            return RedirectToAction(nameof(Login), new { email = user.Email });
        }

        #endregion

        #region --- 4. QUáº¢N LÃ TRANG TÃ€I KHOáº¢N & Sá»” Äá»ŠA CHá»ˆ (UC-05: PROFILE & ADDRESSES) ---

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

        // POST: /Account/Profile (Cáº­p nháº­t thÃ´ng tin cÃ¡ nhÃ¢n)
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

            // Náº¿u ngÆ°á»i dÃ¹ng nháº­p Ä‘á»‹a chá»‰ vÃ  chÆ°a cÃ³ Ä‘á»‹a chá»‰ máº·c Ä‘á»‹nh, lÆ°u vÃ o Addresses
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

            // Cáº­p nháº­t Claims Cookie
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

            TempData["SuccessMessage"] = "Cáº­p nháº­t thÃ´ng tin tÃ i khoáº£n thÃ nh cÃ´ng!";
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
                    .FirstOrDefault() ?? "Dá»¯ liá»‡u Ä‘á»•i máº­t kháº©u khÃ´ng há»£p lá»‡.";

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
                TempData["ErrorMessage"] = "Máº­t kháº©u hiá»‡n táº¡i khÃ´ng chÃ­nh xÃ¡c. Vui lÃ²ng kiá»ƒm tra láº¡i.";
                TempData["ActiveTab"] = "password";
                return RedirectToAction(nameof(Profile), new { tab = "password" });
            }

            user.PasswordHash = _passwordService.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();

            _logger.LogInformation("NgÆ°á»i dÃ¹ng {Email} Ä‘Ã£ Ä‘á»•i máº­t kháº©u thÃ nh cÃ´ng.", user.Email);

            TempData["SuccessMessage"] = "Äá»•i máº­t kháº©u thÃ nh cÃ´ng!";
            TempData["ActiveTab"] = "password";
            return RedirectToAction(nameof(Profile), new { tab = "password" });
        }

        // POST: /Account/AddAddress (ThÃªm Ä‘á»‹a chá»‰ giao hÃ ng má»›i)
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
                TempData["ErrorMessage"] = "Vui lÃ²ng Ä‘iá»n Ä‘áº§y Ä‘á»§ há» tÃªn, sá»‘ Ä‘iá»‡n thoáº¡i vÃ  Ä‘á»‹a chá»‰ nháº­n hÃ ng.";
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

            TempData["SuccessMessage"] = "ÄÃ£ thÃªm Ä‘á»‹a chá»‰ nháº­n hÃ ng má»›i thÃ nh cÃ´ng.";
            return RedirectToAction(nameof(Profile), new { tab = "address" });
        }

        // POST: /Account/UpdateAddress (Sá»­a Ä‘á»‹a chá»‰ giao hÃ ng)
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
                TempData["ErrorMessage"] = "KhÃ´ng tÃ¬m tháº¥y Ä‘á»‹a chá»‰ cáº§n cáº­p nháº­t.";
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

            TempData["SuccessMessage"] = "Cáº­p nháº­t Ä‘á»‹a chá»‰ nháº­n hÃ ng thÃ nh cÃ´ng.";
            return RedirectToAction(nameof(Profile), new { tab = "address" });
        }

        // POST: /Account/DeleteAddress (XÃ³a Ä‘á»‹a chá»‰)
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
                TempData["ErrorMessage"] = "KhÃ´ng tÃ¬m tháº¥y Ä‘á»‹a chá»‰ cáº§n xÃ³a.";
                return RedirectToAction(nameof(Profile), new { tab = "address" });
            }

            // Kiá»ƒm tra rÃ ng buá»™c Ä‘Æ¡n hÃ ng
            bool isUsedInOrders = await _context.Orders.AnyAsync(o => o.AddressId == addressId);
            if (isUsedInOrders)
            {
                TempData["ErrorMessage"] = "Äá»‹a chá»‰ nÃ y Ä‘Ã£ Ä‘Æ°á»£c sá»­ dá»¥ng trong cÃ¡c Ä‘Æ¡n hÃ ng Ä‘Ã£ Ä‘áº·t cá»§a báº¡n, khÃ´ng thá»ƒ xÃ³a Ä‘á»ƒ lÆ°u trá»¯ lá»‹ch sá»­ váº­n chuyá»ƒn.";
                return RedirectToAction(nameof(Profile), new { tab = "address" });
            }

            bool wasDefault = address.IsDefault;
            _context.Addresses.Remove(address);
            await _context.SaveChangesAsync();

            // Náº¿u xÃ³a Ä‘á»‹a chá»‰ máº·c Ä‘á»‹nh, gÃ¡n Ä‘á»‹a chá»‰ Ä‘áº§u tiÃªn cÃ²n láº¡i lÃ m máº·c Ä‘á»‹nh
            if (wasDefault)
            {
                var remaining = await _context.Addresses.FirstOrDefaultAsync(a => a.UserId == userId);
                if (remaining != null)
                {
                    remaining.IsDefault = true;
                    await _context.SaveChangesAsync();
                }
            }

            TempData["SuccessMessage"] = "ÄÃ£ xÃ³a Ä‘á»‹a chá»‰ thÃ nh cÃ´ng.";
            return RedirectToAction(nameof(Profile), new { tab = "address" });
        }

        // POST: /Account/SetDefaultAddress (Äáº·t lÃ m máº·c Ä‘á»‹nh)
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
                TempData["ErrorMessage"] = "KhÃ´ng tÃ¬m tháº¥y Ä‘á»‹a chá»‰.";
                return RedirectToAction(nameof(Profile), new { tab = "address" });
            }

            foreach (var a in addresses)
            {
                a.IsDefault = (a.AddressId == addressId);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "ÄÃ£ cáº­p nháº­t Ä‘á»‹a chá»‰ máº·c Ä‘á»‹nh.";
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
