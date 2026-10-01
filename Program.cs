using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Database - Kết nối SQL Server BakeryManagementDB
var sqlServerConnection = builder.Configuration.GetConnectionString("SqlServerConnection")
    ?? throw new InvalidOperationException("Chưa cấu hình SqlServerConnection trong appsettings.json.");

builder.Services.AddDbContext<BakeryManagementDbContext>(options =>
    options.UseSqlServer(sqlServerConnection));

// 2. Đăng ký các dịch vụ (Dependency Injection)
builder.Services.AddSingleton<IPasswordService, PasswordService>();

// 3. Cấu hình Cookie Authentication & Phân quyền Role
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "BMS.AuthCookie";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(4);
        options.SlidingExpiration = true;
    });

builder.Services.AddControllersWithViews();

var app = builder.Build();

// 4. Cấu hình HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
