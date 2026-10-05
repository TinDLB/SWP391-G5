using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;
using System.Security.Claims;

namespace SWP391_G5.Controllers
{
    [Authorize(Roles = Role.Admin)]
    public class PurchaseOrderController : Controller
    {
        private readonly BakeryManagementDbContext _context;
        private readonly ILogger<PurchaseOrderController> _logger;

        public PurchaseOrderController(BakeryManagementDbContext context, ILogger<PurchaseOrderController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? supplierId, string? status)
        {
            var query = _context.PurchaseOrders
                .Include(po => po.Supplier)
                .Include(po => po.CreatedByNavigation)
                .AsQueryable();

            if (supplierId.HasValue)
                query = query.Where(po => po.SupplierId == supplierId.Value);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(po => po.Status == status);

            var allOrders = await _context.PurchaseOrders.ToListAsync();
            var orders = await query.OrderByDescending(po => po.OrderDate).ToListAsync();
            var suppliers = await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();

            ViewBag.SupplierId = supplierId;
            ViewBag.Status = status;
            ViewBag.Suppliers = suppliers;
            ViewBag.TotalOrders = allOrders.Count;
            ViewBag.TotalDraft = allOrders.Count(o => o.Status == "Draft");
            ViewBag.TotalSent = allOrders.Count(o => o.Status == "Sent");
            ViewBag.TotalPartially = allOrders.Count(o => o.Status == "PartiallyFulfilled");
            ViewBag.TotalCompleted = allOrders.Count(o => o.Status == "Completed");
            ViewBag.TotalAmountSum = allOrders.Sum(o => o.TotalAmount);

            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var po = await _context.PurchaseOrders
                .Include(p => p.Supplier)
                .Include(p => p.CreatedByNavigation)
                .Include(p => p.PurchaseOrderItems)
                    .ThenInclude(i => i.Ingredient)
                .Include(p => p.GoodsReceipts)
                .FirstOrDefaultAsync(p => p.PoId == id);

            if (po == null)
                return NotFound();

            var progress = await _context.VwPurchaseOrderProgresses
                .Where(v => v.PoId == id)
                .ToListAsync();

            ViewBag.Progress = progress;

            return View(po);
        }

        public async Task<IActionResult> Create()
        {
            var model = new CreatePurchaseOrderViewModel();
            ViewBag.Suppliers = await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
            ViewBag.Ingredients = await _context.Ingredients.Where(i => i.IsActive).OrderBy(i => i.Name).ToListAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePurchaseOrderViewModel model)
        {
            if (!ModelState.IsValid || model.Items == null || !model.Items.Any())
            {
                if (model.Items == null || !model.Items.Any())
                    ModelState.AddModelError("Items", "Vui lòng thêm ít nhất một nguyên liệu vào đơn hàng.");

                ViewBag.Suppliers = await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
                ViewBag.Ingredients = await _context.Ingredients.Where(i => i.IsActive).OrderBy(i => i.Name).ToListAsync();
                return View(model);
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return Forbid();

            var today = DateTime.Now.ToString("yyyyMMdd");
            var todayCount = await _context.PurchaseOrders
                .CountAsync(po => po.PoCode.StartsWith($"PO-{today}-"));
            var poCode = $"PO-{today}-{(todayCount + 1):D3}";

            var totalAmount = model.Items.Sum(i => i.OrderedQty * i.UnitPrice);

            var po = new PurchaseOrder
            {
                PoCode = poCode,
                SupplierId = model.SupplierId,
                CreatedBy = userId,
                Status = "Draft",
                OrderDate = DateTime.Now,
                ExpectedDate = model.ExpectedDate,
                TotalAmount = totalAmount,
                Note = model.Note,
                PurchaseOrderItems = model.Items.Select(i => new PurchaseOrderItem
                {
                    IngredientId = i.IngredientId,
                    OrderedQty = i.OrderedQty,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };

            _context.PurchaseOrders.Add(po);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Đã tạo đơn mua hàng {PoCode} bởi UserId={UserId}", poCode, userId);
            TempData["SuccessMessage"] = $"Đã tạo thành công đơn đặt hàng {poCode}.";

            return RedirectToAction(nameof(Details), new { id = po.PoId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            var allowed = new[] { "Sent", "Cancelled" };
            if (!allowed.Contains(newStatus))
            {
                TempData["ErrorMessage"] = "Trạng thái không hợp lệ.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var po = await _context.PurchaseOrders.FindAsync(id);
            if (po == null) return NotFound();

            po.Status = newStatus;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đơn hàng {po.PoCode} đã chuyển sang trạng thái '{newStatus}'.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}