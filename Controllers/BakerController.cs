using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SWP391_G5.Data;

namespace SWP391_G5.Controllers
{
    [Authorize(Roles = "Baker")]
    public class BakerController : Controller
    {
        private readonly BakeryManagementDbContext _context;

        public BakerController(BakeryManagementDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}