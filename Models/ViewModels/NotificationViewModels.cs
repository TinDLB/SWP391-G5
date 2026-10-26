namespace SWP391_G5.Models.ViewModels
{
    public class NotificationListViewModel
    {
        public int UnreadCount { get; set; }
        public List<NotificationItemViewModel> Notifications { get; set; } = new List<NotificationItemViewModel>();
    }

    public class NotificationItemViewModel
    {
        public int NotificationId { get; set; }
        public string NotificationType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        public string TimeAgo
        {
            get
            {
                var span = DateTime.UtcNow - CreatedAt;
                if (span.TotalMinutes < 1) return "Vừa xong";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} phút trước";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours} giờ trước";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays} ngày trước";
                return CreatedAt.ToString("dd/MM/yyyy HH:mm");
            }
        }

        public string IconClass => NotificationType switch
        {
            "OrderPlaced" => "bi-bag-check-fill text-primary",
            "OrderDelivering" => "bi-truck text-warning",
            "OrderDelivered" => "bi-check-circle-fill text-success",
            "LowStock" => "bi-exclamation-triangle-fill text-danger",
            _ => "bi-bell-fill text-info"
        };
    }
}
