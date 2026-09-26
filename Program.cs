using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Database (Hỗ trợ chuyển đổi giữa SQLite và SQL Server)
var databaseProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";

if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    var sqlServerConnection = builder.Configuration.GetConnectionString("SqlServerConnection") 
        ?? throw new InvalidOperationException("Chưa cấu hình SqlServerConnection trong appsettings.json.");
    builder.Services.AddDbContext<BakeryDbContext>(options =>
        options.UseSqlServer(sqlServerConnection));
}
else
{
    var sqliteConnection = builder.Configuration.GetConnectionString("SqliteConnection") 
        ?? "Data Source=bakery.db";
    builder.Services.AddDbContext<BakeryDbContext>(options =>
        options.UseSqlite(sqliteConnection));
}

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

// 4. Tự động khởi tạo Database và nạp dữ liệu mẫu (Seed Data)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<BakeryDbContext>();
        var passwordService = services.GetRequiredService<IPasswordService>();
        DbInitializer.Initialize(context, passwordService);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Lỗi xảy ra trong quá trình khởi tạo CSDL.");
    }
}

// 5. Cấu hình HTTP request pipeline
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
