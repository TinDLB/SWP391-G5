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
public class GoodsReceiptController(BakeryManagementDbContext db, PurchasingService service, ILogger<GoodsReceiptController> logger) : Controller
{
    public async Task<IActionResult> Create(int poId)
    {
        var p = await service.Orders.AsNoTracking().FirstOrDefaultAsync(p => p.PoId == poId);
        if (p == null) return NotFound();
        if (!PurchasingService.CanReceive(p)) { TempData["ErrorMessage"] = "Đơn này không được nhận hàng."; return RedirectToAction("Details", "PurchaseOrder", new { id = poId }); }
        ViewBag.Po = p;
        return View(new CreateGoodsReceiptViewModel { PoId = poId, Items = p.PurchaseOrderItems.OrderBy(i => i.PoItemId).Select(i => new GoodsReceiptInput { PoItemId = i.PoItemId }).ToList() });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateGoodsReceiptViewModel model)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) return Forbid();
        if (ModelState.IsValid)
        {
            try { var id = await service.Receive(model, uid); TempData["SuccessMessage"] = "Đã nhập kho phần nhận đạt và cập nhật tiến độ PO."; return RedirectToAction(nameof(Details), new { id }); }
            catch (InvalidOperationException e) { ModelState.AddModelError("", e.Message); }
            catch (DbUpdateException e) { logger.LogError(e, "Receipt failed"); ModelState.AddModelError("", "Không lưu được phiếu. Toàn bộ cập nhật đã được hoàn tác; hãy tải lại PO."); }
            catch (OverflowException) { ModelState.AddModelError("", "Số lượng hoặc chi phí vượt giới hạn."); }
        }
        db.ChangeTracker.Clear();
        var p = await service.Orders.AsNoTracking().FirstOrDefaultAsync(p => p.PoId == model.PoId);
        if (p == null) return NotFound();
        if (!PurchasingService.CanReceive(p)) { TempData["ErrorMessage"] = "PO không còn được nhận hàng."; return RedirectToAction("Details", "PurchaseOrder", new { id = model.PoId }); }
        if (model.Items == null || model.Items.Select(i => i.PoItemId).Distinct().Count() != model.Items.Count || model.Items.Any(i => !p.PurchaseOrderItems.Any(x => x.PoItemId == i.PoItemId)))
        { ModelState.Clear(); ModelState.AddModelError("", "Danh sách dòng nhận không hợp lệ."); model.Items = p.PurchaseOrderItems.Select(i => new GoodsReceiptInput { PoItemId = i.PoItemId }).ToList(); }
        ViewBag.Po = p; return View(model);
    }
    public async Task<IActionResult> Details(int id)
    {
        var r = await db.GoodsReceipts.AsNoTracking().Include(r => r.ReceivedByNavigation).Include(r => r.GoodsReceiptItems).ThenInclude(i => i.PoItem).ThenInclude(i => i.Ingredient).FirstOrDefaultAsync(r => r.ReceiptId == id);
        if (r == null) return NotFound();
        r.Po = (await service.Orders.AsNoTracking().FirstAsync(p => p.PoId == r.PoId));
        return View(r);
    }
}
