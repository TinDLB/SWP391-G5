using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SWP391_G5.Models.ViewModels;
using SWP391_G5.Services;

namespace SWP391_G5.Controllers
{
    [Authorize(Roles = "Baker,Admin")]
    public class IngredientController : Controller
    {
        private readonly IIngredientService _service;

        public IngredientController(IIngredientService service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> Index(string? keyword, string? filter)
            => View(await _service.GetListAsync(keyword, filter));

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _service.GetDetailAsync(id);
            if (model == null) return NotFound();
            return View(model);
        }

        [HttpGet]
        public IActionResult Create() => View("Form", new IngredientFormViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IngredientFormViewModel model)
        {
            if (!ModelState.IsValid) return View("Form", model);

            var result = await _service.CreateAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View("Form", model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _service.GetFormAsync(id);
            if (model == null) return NotFound();
            return View("Form", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(IngredientFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await RestoreReadOnlyFieldsAsync(model);
                return View("Form", model);
            }

            var result = await _service.UpdateAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                await RestoreReadOnlyFieldsAsync(model);
                return View("Form", model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id = model.IngredientId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(int id, bool isActive)
        {
            var result = await _service.SetActiveAsync(id, isActive);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        // Các trường chỉ đọc không đi theo form, nên phải lấy lại từ DB khi hiện lại form lỗi
        private async Task RestoreReadOnlyFieldsAsync(IngredientFormViewModel model)
        {
            var current = await _service.GetFormAsync(model.IngredientId);
            if (current == null) return;
            model.StockQty = current.StockQty;
            model.UnitLocked = current.UnitLocked;
        }
    }
}
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;
namespace SWP391_G5.Controllers;
[Authorize(Roles=Role.Admin)]
public class IngredientController(BakeryManagementDbContext db, ILogger<IngredientController> logger) : Controller
{
    public async Task<IActionResult> Index(string? search, string? status, bool lowStock=false)
    {
        var query=db.Ingredients.AsNoTracking();
        search=search?.Trim();
        if (!string.IsNullOrEmpty(search)) query=query.Where(i=>i.Name.Contains(search) || i.Unit.Contains(search));
        if (status=="active") query=query.Where(i=>i.IsActive);
        if (status=="inactive") query=query.Where(i=>!i.IsActive);
        if (lowStock) query=query.Where(i=>i.StockQty<=i.MinThreshold);
        return View(new IngredientListViewModel { Search=search, Status=status, LowStock=lowStock, Items=await query.OrderBy(i=>i.Name).ToListAsync() });
    }
    public IActionResult Create() => View("Edit",new IngredientInputModel());
    public async Task<IActionResult> Edit(int id)
    {
        var item=await db.Ingredients.AsNoTracking().SingleOrDefaultAsync(i=>i.IngredientId==id);
        if(item==null) return NotFound();
        ViewBag.EditId=id;
        return View(new IngredientInputModel {Name=item.Name,Unit=item.Unit,MinThreshold=item.MinThreshold});
    }
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create(IngredientInputModel model) => Save(model,null);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int id, IngredientInputModel model) => Save(model,id);
    private async Task<IActionResult> Save(IngredientInputModel model,int? id)
    {
        model.Name=model.Name?.Trim() ?? ""; model.Unit=model.Unit?.Trim() ?? "";
        if(string.IsNullOrWhiteSpace(model.Name)) ModelState.AddModelError(nameof(model.Name),"Nhập tên nguyên liệu.");
        if(string.IsNullOrWhiteSpace(model.Unit)) ModelState.AddModelError(nameof(model.Unit),"Nhập đơn vị tính.");
        if(decimal.Round(model.MinThreshold,3)!=model.MinThreshold) ModelState.AddModelError(nameof(model.MinThreshold),"Ngưỡng tối thiểu có tối đa 3 chữ số thập phân.");
        if(await db.Ingredients.AnyAsync(i=>i.IngredientId!=id && i.Name.ToLower()==model.Name.ToLower())) ModelState.AddModelError(nameof(model.Name),"Tên nguyên liệu đã tồn tại.");
        var item=id.HasValue ? await db.Ingredients.SingleOrDefaultAsync(i=>i.IngredientId==id.Value) : new Ingredient {IsActive=true,StockQty=0};
        if(item==null) return NotFound();
        // Existing quantities and historical records use this unit; changing it would reinterpret stock.
        if(id.HasValue && item.Unit!=model.Unit && (item.StockQty!=0 || await db.InventoryTransactions.AnyAsync(t=>t.IngredientId==id.Value)
            || await db.RecipeItems.AnyAsync(t=>t.IngredientId==id.Value) || await db.PurchaseOrderItems.AnyAsync(t=>t.IngredientId==id.Value)
            || await db.StockAdjustments.AnyAsync(t=>t.IngredientId==id.Value) || await db.StockCountItems.AnyAsync(t=>t.IngredientId==id.Value)
            || await db.ProductionTaskIngredients.AnyAsync(t=>t.IngredientId==id.Value)))
            ModelState.AddModelError(nameof(model.Unit),"Không thể đổi đơn vị khi đã có tồn kho hoặc dữ liệu sử dụng nguyên liệu.");
        if(ModelState.IsValid)
        {
            item.Name=model.Name;item.Unit=model.Unit;item.MinThreshold=model.MinThreshold;
            if(!id.HasValue) db.Ingredients.Add(item);
            try {await db.SaveChangesAsync();TempData["SuccessMessage"]="Đã lưu nguyên liệu.";return RedirectToAction(nameof(Index));}
            catch(DbUpdateException e) {logger.LogError(e,"Save ingredient failed");ModelState.AddModelError("","Không thể lưu nguyên liệu. Tên có thể đã tồn tại; vui lòng tải lại và thử lại.");}
        }
        ViewBag.EditId=id;return View("Edit",model);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int id,bool isActive)
    {
        try
        {
            var affected=await db.Ingredients.Where(i=>i.IngredientId==id).ExecuteUpdateAsync(s=>s.SetProperty(i=>i.IsActive,isActive));
            if(affected==0) return NotFound();
            TempData["SuccessMessage"]=isActive ? "Đã kích hoạt nguyên liệu." : "Đã ngừng sử dụng nguyên liệu.";
        }
        catch(DbUpdateException e) {logger.LogError(e,"Ingredient status failed");TempData["ErrorMessage"]="Không thể cập nhật trạng thái. Vui lòng thử lại.";}
        return RedirectToAction(nameof(Index));
    }
}
