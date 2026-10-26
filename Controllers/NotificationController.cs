using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SWP391_G5.Services;

namespace SWP391_G5.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // GET: /Notification/Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = await _notificationService.GetUserNotificationsAsync(userId.Value, 30);
            return View(model);
        }

        // GET: /Notification/GetPopup (AJAX cho chuông thông báo)
        [HttpGet]
        public async Task<IActionResult> GetPopup()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Json(new { unreadCount = 0, notifications = new object[] { } });
            }

            var model = await _notificationService.GetUserNotificationsAsync(userId.Value, 5);
            return Json(new
            {
                unreadCount = model.UnreadCount,
                notifications = model.Notifications
            });
        }

        // POST: /Notification/MarkAsRead/5
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Json(new { success = false });
            }

            var result = await _notificationService.MarkAsReadAsync(id, userId.Value);
            return Json(new { success = result });
        }

        // POST: /Notification/MarkAllAsRead
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Json(new { success = false });
            }

            await _notificationService.MarkAllAsReadAsync(userId.Value);
            return Json(new { success = true });
        }

        private int? GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                return userId;
            }
            return null;
        }
    }
}
