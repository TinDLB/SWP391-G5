using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;
using SWP391_G5.Services;
namespace SWP391_G5.Controllers;

[Authorize(Roles = Role.Admin)]
public class StockAdjustmentController(BakeryManagementDbContext db, StockAdjustmentService service, ILogger<StockAdjustmentController> logger) : Controller
{
    private async Task Load(StockAdjustmentViewModel model)
    {
        model.Ingredients = await db.Ingredients.AsNoTracking().Where(i => i.IsActive).OrderBy(i => i.Name).ToListAsync();
        model.History = await db.StockAdjustments.AsNoTracking().Include(a => a.Ingredient).Include(a => a.CreatedByNavigation)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AdjustmentId).Take(100).ToListAsync();
    }
    public async Task<IActionResult> Index(int? ingredientId)
    {
        var model = new StockAdjustmentViewModel();
        await Load(model);
        if (ingredientId.HasValue && model.Ingredients.Any(i => i.IngredientId == ingredientId.Value))
        {
            model.IngredientId = ingredientId.Value;
        }
        return View(model);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StockAdjustmentViewModel model)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                await service.Adjust(model, uid);
                TempData["SuccessMessage"] = "Đã điều chỉnh tồn kho và lưu lịch sử.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException e) { ModelState.AddModelError("", e.Message); }
            catch (DbUpdateException e) { logger.LogError(e, "Stock adjustment failed"); ModelState.AddModelError("", "Không lưu được điều chỉnh. Vui lòng tải lại và thử lại."); }
        }
        db.ChangeTracker.Clear();
        await Load(model);
        return View("Index", model);
    }
}
