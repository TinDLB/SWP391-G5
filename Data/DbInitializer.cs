using SWP391_G5.Models.Entities;
using SWP391_G5.Services;

namespace SWP391_G5.Data
{
    /// <summary>
    /// DbInitializer không còn được sử dụng khi kết nối BakeryManagementDB (database đã có sẵn dữ liệu).
    /// Giữ lại class này để tương thích nếu cần seed dữ liệu cho môi trường dev/test sau này.
    /// </summary>
    public static class DbInitializer
    {
        public static void Initialize(BakeryManagementDbContext context, IPasswordService passwordService)
        {
            // BakeryManagementDB đã có dữ liệu sẵn — không cần seed.
        }
    }
}
