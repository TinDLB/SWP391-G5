using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;
using SWP391_G5.Services;
namespace SWP391_G5.Controllers;
[Authorize(Roles = Role.Admin)]
public class PurchaseOrderController(BakeryManagementDbContext db, PurchasingService service, ILogger<PurchaseOrderController> logger) : Controller
{
    public async Task<IActionResult> Index(int? supplierId, string? status, string? search, DateTime? from, DateTime? to, DateOnly? expectedDate)
    {
        var q = service.Orders.AsNoTracking();
        if (supplierId.HasValue) q = q.Where(p => p.SupplierId == supplierId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(p => p.PoCode.Contains(search) || p.Supplier.Name.Contains(search));
        if (from.HasValue) q = q.Where(p => p.OrderDate >= from.Value.Date);
        if (to.HasValue) q = q.Where(p => p.OrderDate < to.Value.Date.AddDays(1));
        if (expectedDate.HasValue) q = q.Where(p => p.ExpectedDate == expectedDate);
        ViewBag.Suppliers = await db.Suppliers.OrderBy(s => s.Name).ToListAsync();
        ViewBag.SupplierId = supplierId; ViewBag.Status = status;
        var all = await db.PurchaseOrders.AsNoTracking().ToListAsync();
        ViewBag.TotalOrders = all.Count; ViewBag.TotalDraft = all.Count(p => p.Status == "Draft"); ViewBag.TotalSent = all.Count(p => p.Status == "Sent"); ViewBag.TotalPartially = all.Count(p => p.Status == "PartiallyFulfilled"); ViewBag.TotalCompleted = all.Count(p => p.Status == "Completed"); ViewBag.TotalAmountSum = all.Sum(p => p.TotalAmount);
        return View(await q.OrderByDescending(p => p.OrderDate).ToListAsync());
    }
    public async Task<IActionResult> Details(int id)
    {
        var p = await service.Orders.AsNoTracking().FirstOrDefaultAsync(p => p.PoId == id);
        if (p == null) return NotFound();
        ViewBag.Progress = p.PurchaseOrderItems.Select(i => new VwPurchaseOrderProgress { PoId = id, PoCode = p.PoCode, PoStatus = p.Status, PoItemId = i.PoItemId, IngredientName = i.Ingredient.Name, Unit = i.Ingredient.Unit, OrderedQty = i.OrderedQty, AcceptedQty = PurchasingService.Accepted(i), RemainingQty = i.OrderedQty - PurchasingService.Accepted(i) }).ToList();
        return View(p);
    }
    private async Task Options()
    {
        ViewBag.Suppliers = await db.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
        ViewBag.Ingredients = await db.Ingredients.Where(i => i.IsActive).OrderBy(i => i.Name).ToListAsync();
    }
    public async Task<IActionResult> Create() { await Options(); return View(new CreatePurchaseOrderViewModel { Items = new() { new() } }); }
    public async Task<IActionResult> Edit(int id)
    {
        var p = await service.Orders.AsNoTracking().FirstOrDefaultAsync(p => p.PoId == id);
        if (p == null) return NotFound();
        if (p.Status != "Draft" || p.GoodsReceipts.Any()) return BadRequest("Chỉ sửa PO nháp chưa nhận hàng.");
        ViewBag.EditId = id; await Options();
        return View("Create", new CreatePurchaseOrderViewModel { SupplierId = p.SupplierId, OrderDate = p.OrderDate, ExpectedDate = p.ExpectedDate, Note = p.Note, Items = p.PurchaseOrderItems.Select(i => new PurchaseOrderItemInputModel { IngredientId = i.IngredientId, OrderedQty = i.OrderedQty, UnitPrice = i.UnitPrice }).ToList() });
    }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Create(CreatePurchaseOrderViewModel model) => Save(model, null);
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Edit(int id, CreatePurchaseOrderViewModel model) => Save(model, id);
    private async Task<IActionResult> Save(CreatePurchaseOrderViewModel m, int? id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) return Forbid();
        if (ModelState.IsValid)
        {
            try { var saved = await service.SaveOrder(m, uid, id); TempData["SuccessMessage"] = "Đã lưu đơn mua hàng."; return RedirectToAction(nameof(Details), new { id = saved }); }
            catch (InvalidOperationException e) { ModelState.AddModelError("", e.Message); }
            catch (DbUpdateException e) { logger.LogError(e, "Save PO failed"); ModelState.AddModelError("", "Không lưu được đơn. Vui lòng tải lại và thử lại."); }
            catch (OverflowException) { ModelState.AddModelError("", "Số lượng hoặc tổng tiền quá lớn."); }
        }
        ViewBag.EditId = id; await Options(); return View("Create", m);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string newStatus)
    {
        try { await service.ChangeStatus(id, newStatus); TempData["SuccessMessage"] = "Đã cập nhật trạng thái đơn."; }
        catch (InvalidOperationException e) { TempData["ErrorMessage"] = e.Message; }
        catch (DbUpdateException e) { logger.LogError(e, "Status update failed"); TempData["ErrorMessage"] = "Không thể cập nhật; vui lòng tải lại đơn."; }
        return RedirectToAction(nameof(Details), new { id });
    }
}
