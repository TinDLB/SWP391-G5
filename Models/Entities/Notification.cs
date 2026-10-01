using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class Notification
{
    public int NotificationId { get; set; }

    public int UserId { get; set; }

    public string NotificationType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Message { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
