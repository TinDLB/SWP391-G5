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
            // UI: Views/Ingredient/Index.cshtml gửi GET /Ingredient/Index?keyword=...&filter=...
            // Controller nhận 2 query parameter này, truyền nguyên trạng vào service.
            // Service đọc bảng Ingredients và trả IngredientListViewModel; Controller đưa model
            // vào View(model), sau đó Razor render lại danh sách lên UI.
            => View(await _service.GetListAsync(keyword, filter));

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            // UI truyền id qua link asp-route-id -> route /Ingredient/Details/{id}.
            // Controller nhận id, service query Ingredients, RecipeItems, ProductionTasks,
            // ProductionTaskIngredients và InventoryTransactions, rồi trả ViewModel cho UI.
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
            // UI submit Form.cshtml bằng POST; model binding nhận Name, Unit, MinThreshold
            // vào IngredientFormViewModel. Service kiểm tra rồi INSERT vào Ingredients.
            // Kết quả ServiceResult được Controller chuyển thành lỗi ModelState hoặc redirect Index.
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
            // UI gửi id từ link Edit; Controller chuyển id cho service query Ingredients,
            // rồi truyền IngredientFormViewModel vào lại Form.cshtml để hiển thị.
            var model = await _service.GetFormAsync(id);
            if (model == null) return NotFound();
            return View("Form", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(IngredientFormViewModel model)
        {
            // UI POST Form.cshtml gửi IngredientId, Name, Unit, MinThreshold.
            // Controller nhận model, service đọc bản ghi Ingredients theo IngredientId,
            // kiểm tra rồi UPDATE Ingredients. Khi lỗi, Controller query lại dữ liệu chỉ đọc
            // (StockQty/UnitLocked) và truyền model lỗi ngược về Form.cshtml.
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
            // UI gửi hidden id/isActive từ Details.cshtml bằng POST.
            // Controller nhận 2 giá trị, service query rồi UPDATE Ingredients.IsActive,
            // sau đó redirect về /Ingredient/Details/{id}; Details GET query lại và render UI.
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
