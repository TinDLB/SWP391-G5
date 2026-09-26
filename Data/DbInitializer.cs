using SWP391_G5.Models.Entities;
using SWP391_G5.Services;

namespace SWP391_G5.Data
{
    public static class DbInitializer
    {
        public static void Initialize(BakeryDbContext context, IPasswordService passwordService)
        {
            context.Database.EnsureCreated();

            if (context.Users.Any())
            {
                return; // DB đã có dữ liệu
            }

            var defaultPasswordHash = passwordService.HashPassword("123456");

            var users = new List<User>
            {
                new User
                {
                    FullName = "Nguyễn Khách Hàng",
                    Email = "customer@bakery.com",
                    PasswordHash = defaultPasswordHash,
                    PhoneNumber = "0912345678",
                    Address = "123 Đường Hoa Hồng, Phường Bến Nghé, Quận 1, TP.HCM",
                    Role = Role.Customer,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    FullName = "Trần Thu Ngân (Cashier)",
                    Email = "cashier@bakery.com",
                    PasswordHash = defaultPasswordHash,
                    PhoneNumber = "0987654321",
                    Address = "Quầy POS Tiệm Bánh BMS",
                    Role = Role.Cashier,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    FullName = "Lê Đầu Bếp (Baker)",
                    Email = "baker@bakery.com",
                    PasswordHash = defaultPasswordHash,
                    PhoneNumber = "0933445566",
                    Address = "Xưởng Nướng Bánh Trung Tâm",
                    Role = Role.Baker,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    FullName = "Quản Trị Viên (Admin)",
                    Email = "admin@bakery.com",
                    PasswordHash = defaultPasswordHash,
                    PhoneNumber = "0909090909",
                    Address = "Trụ Sở Bakery Management System",
                    Role = Role.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            context.Users.AddRange(users);
            context.SaveChanges();
        }
    }
}
