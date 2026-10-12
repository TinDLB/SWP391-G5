
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userIdClaim = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out int bakerId))
            {
                return Unauthorized();
            }

            var tasks = await _bakerService
                .GetAssignedTasksAsync(bakerId);

            return View(tasks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartProduction(int id)
        {
            var userIdClaim = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out int bakerId))
            {
                return Unauthorized();
            }

            var success = await _bakerService
                .StartProductionAsync(id, bakerId);

            if (success)
            {
                TempData["SuccessMessage"] =
                    $"Production task #{id} started successfully.";
            }
            else
            {
                TempData["ErrorMessage"] =
                    "Unable to start this task. Please check its assignment and status.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}