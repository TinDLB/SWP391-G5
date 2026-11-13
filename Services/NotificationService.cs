using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;

namespace SWP391_G5.Services
{
    public class NotificationService : INotificationService
    {
        private readonly BakeryManagementDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(BakeryManagementDbContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task NotifyAdminsAsync(string type, string title, string? message)
        {
            // Đọc UserId của admin từ Users, nhận nội dung thông báo từ luồng nghiệp vụ,
            // rồi ghi từng thông báo vào Notifications.
            try
            {
                var adminIds = await _context.Users
                    .Where(u => u.Role == "Admin" && u.IsActive)
                    .Select(u => u.UserId)
                    .ToListAsync();

                foreach (var adminId in adminIds)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = adminId,
                        NotificationType = type,
                        Title = title,
                        Message = message,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (adminIds.Count > 0)
                    await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Không để lỗi ghi Notifications làm hỏng thao tác chính như tạo task/voucher.
                _logger.LogError(ex, "Lỗi khi tạo thông báo cho Admin: {NotificationType}", type);
            }
        }

        public async Task CreateNotificationAsync(int userId, string type, string title, string? message)
        {
            // userId/type/title/message được truyền từ controller/service; lưu vào Notifications
            // để hiển thị lại trong NotificationController và Views/Notification/Index.cshtml.
            try
            {
                var notif = new Notification
                {
                    UserId = userId,
                    NotificationType = type,
                    Title = title,
                    Message = message,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notif);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo thông báo cho UserId {UserId}", userId);
            }
        }

        public async Task<NotificationListViewModel> GetUserNotificationsAsync(int userId, int limit = 15)
        {
            var notifs = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .Select(n => new NotificationItemViewModel
                {
                    NotificationId = n.NotificationId,
                    NotificationType = n.NotificationType,
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            var unreadCount = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync();

            return new NotificationListViewModel
            {
                UnreadCount = unreadCount,
                Notifications = notifs
            };
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync();
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
        {
            var notif = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

            if (notif != null && !notif.IsRead)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            var unreadList = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var item in unreadList)
            {
                item.IsRead = true;
            }

            await _context.SaveChangesAsync();
        }
    }
}
