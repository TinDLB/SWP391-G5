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

        public async Task<IActionResult> Index()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out int bakerId))
            {
                return Unauthorized();
            }

            var tasks = await _bakerService.GetAssignedTasksAsync(bakerId);

            return View(tasks);
        }
    }
}