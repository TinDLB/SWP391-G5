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