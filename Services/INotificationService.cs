using SWP391_G5.Models.ViewModels;

namespace SWP391_G5.Services
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(int userId, string type, string title, string? message);
        Task<NotificationListViewModel> GetUserNotificationsAsync(int userId, int limit = 15);
        Task<int> GetUnreadCountAsync(int userId);
        Task<bool> MarkAsReadAsync(int notificationId, int userId);
        Task MarkAllAsReadAsync(int userId);
    }
}
