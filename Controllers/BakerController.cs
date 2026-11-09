using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SWP391_G5.Models.ViewModels;
using SWP391_G5.Services;

namespace SWP391_G5.Controllers
{
    [Authorize(Roles = "Baker")]
    public class BakerController : Controller
    {
        private readonly IBakerService _bakerService;

        public BakerController(IBakerService bakerService)
        {
            _bakerService = bakerService;
        }

        // ─────────────────────────────────────────
        // GET /Baker  →  Danh sách task của baker
        // ─────────────────────────────────────────
        public async Task<IActionResult> Index()
        {
            var bakerId = GetBakerId();
            if (bakerId == null) return Unauthorized();

            var tasks = await _bakerService.GetAssignedTasksAsync(bakerId.Value);
            return View(tasks);
        }

        // ─────────────────────────────────────────
        // GET /Baker/TaskDetail/{id}  →  Chi tiết task + nguyên liệu
        // ─────────────────────────────────────────
        public async Task<IActionResult> TaskDetail(int id)
        {
            var bakerId = GetBakerId();
            if (bakerId == null) return Unauthorized();

            var model = await _bakerService.GetTaskDetailAsync(id, bakerId.Value);
            if (model == null) return NotFound();

            return View(model);
        }

        // ─────────────────────────────────────────
        // POST /Baker/StartTask  →  Bắt đầu sản xuất
        // ─────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartTask(int taskId)
        {
            var bakerId = GetBakerId();
            if (bakerId == null) return Unauthorized();

            var result = await _bakerService.StartTaskAsync(taskId, bakerId.Value);

            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(TaskDetail), new { id = taskId });
        }

        // ─────────────────────────────────────────
        // GET /Baker/CompleteTask/{id}  →  Form nhập kết quả
        // ─────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> CompleteTask(int id)
        {
            var bakerId = GetBakerId();
            if (bakerId == null) return Unauthorized();

            var detail = await _bakerService.GetTaskDetailAsync(id, bakerId.Value);
            if (detail == null) return NotFound();

            if (detail.Status != "InProgress")
            {
                TempData["ErrorMessage"] = "Chỉ có thể hoàn tất task đang thực hiện.";
                return RedirectToAction(nameof(TaskDetail), new { id });
            }

            var model = new CompleteTaskViewModel
            {
                TaskId      = detail.TaskId,
                ProductName = detail.ProductName,
                TargetQty   = detail.TargetQty
            };
            return View(model);
        }

        // ─────────────────────────────────────────
        // POST /Baker/CompleteTask  →  Lưu kết quả hoàn tất
        // ─────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteTask(CompleteTaskViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var bakerId = GetBakerId();
            if (bakerId == null) return Unauthorized();

            var result = await _bakerService.CompleteTaskAsync(model, bakerId.Value);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────
        // Helper
        // ─────────────────────────────────────────
        private int? GetBakerId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out int id) ? id : null;
        }
    }
}
