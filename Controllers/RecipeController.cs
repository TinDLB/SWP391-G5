using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;

namespace SWP391_G5.Controllers;

[Authorize(Roles = "Admin")]
public class RecipeController : Controller
{
    private readonly BakeryManagementDbContext _context;

    public RecipeController(BakeryManagementDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? search, string? status) //Trả về danh sách công thức dựa trên từ khóa tìm kiếm và trạng thái hoạt động
    {
        // UI gửi search/status qua query string từ Views/Recipe/Index.cshtml.
        // Controller query Recipes, Products và RecipeItems, map sang RecipeListItemViewModel,
        // rồi truyền danh sách bằng View(recipes) ngược lại cho Razor render.
        // Query từ bảng Recipes lấy thông tin bánh liên kết với công thức
        var query = _context.Recipes
            .Include(r => r.Product)
            .Include(r => r.RecipeItems)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r => r.Product.Name.ToLower().Contains(search.Trim().ToLower())); //Tìm kiếm công thức chứa từ khóa

        if (status == "active") query = query.Where(r => r.IsActive); 
        if (status == "inactive") query = query.Where(r => !r.IsActive);

        ViewBag.Search = search;
        ViewBag.Status = status;
        var recipes = await query.OrderBy(r => r.Product.Name)
            .Select(r => new RecipeListItemViewModel
            {
                RecipeId = r.RecipeId,
                ProductId = r.ProductId,
                ProductName = r.Product.Name,
                IngredientCount = r.RecipeItems.Count,
                IsActive = r.IsActive
            }) //Truyền dữ liệu qua query
            .ToListAsync(); //Thực thi query thành List

        return View(recipes); //trả dữ liệu lên giao diện
    }

    public async Task<IActionResult> Details(int id)
    {
        // Nhận RecipeId từ route; đọc Recipes, Products, RecipeItems và Ingredients,
        // rồi truyền entity recipe đã nạp quan hệ sang View chi tiết.
        var recipe = await _context.Recipes
            .Include(r => r.Product)
            .Include(r => r.RecipeItems).ThenInclude(i => i.Ingredient)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RecipeId == id); //Trả dữ liệu công thức dựa trên RecipeId, bao gồm thông tin bánh và danh sách nguyên liệu liên quan

        return recipe is null ? NotFound() : View(recipe);
    }

    //Mở form
    public async Task<IActionResult> Create()
    {
        // GET chỉ nạp Products và Ingredients đang active vào ViewBag để UI tạo form.
        await LoadOptionsAsync(); //Lấy danh sách bánh và nguyên liệu
        return View(new RecipeEditViewModel());
    }

    //Nhận dữ liệu
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RecipeEditViewModel model)
    {
        // POST nhận ProductId, Instructions, IsActive và danh sách Ingredients từ form UI.
        // Dữ liệu hợp lệ được map thành Recipe và RecipeItems, sau đó lưu vào Recipes/RecipeItems.
        // Nh?n ProductId, YieldQty, Instructions v� danh s�ch Ingredients t? form t?o c�ng th?c.
        NormalizeIngredients(model);
        await ValidateRecipeAsync(model);
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return View(model);
        }

        //Nếu dữ liệu hợp lệ, tạo entity Recipe lưu vào bảng Recipe
        var recipe = new Recipe
        {
            ProductId = model.ProductId,
            YieldQty = 1,
            Instructions = model.Instructions?.Trim(),
            IsActive = model.IsActive,
            RecipeItems = model.Ingredients.Select(i => new RecipeItem //Mỗi nguyên liệu được chuyển thành 1 RecipeItem và lưu vào bảng RecipeItems
            {
                IngredientId = i.IngredientId,
                Quantity = i.Quantity 
            }).ToList()
        };

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã tạo công thức thành công.";
        return RedirectToAction(nameof(Index));
    }

    //Mở form chỉnh sửa
    public async Task<IActionResult> Edit(int id)
    {
        var recipe = await _context.Recipes 
            .Include(r => r.RecipeItems).ThenInclude(i => i.Ingredient)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RecipeId == id); //Lấy công thức từ bảng Recipes dựa trên RecipeId, bao gồm danh sách nguyên liệu liên quan

        if (recipe is null) return NotFound();
        await LoadOptionsAsync(recipe.ProductId);
        return View(ToViewModel(recipe));
    }

    //Nhận dữ liệu
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RecipeEditViewModel model)
    {
        // Nhận id từ route và model từ form; query Recipes theo RecipeId, sau đó UPDATE Recipes
        // và thay thế các dòng liên quan trong RecipeItems. Kết quả redirect về danh sách UI.
        // Nhận id từ route và RecipeEditViewModel từ form; cập nhật Recipes,
        // xóa RecipeItems cũ và tạo lại RecipeItems theo danh sách nguyên liệu gửi lên.
        if (id != model.RecipeId) return BadRequest(); //Đảm bảo RepId truyền từ route và form là giống nhau
        NormalizeIngredients(model);
        await ValidateRecipeAsync(model, id);
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(model.ProductId);
            return View(model);
        } //Nếu lỗi thì load lại dropdown và trả lại form cùng lỗi

        var recipe = await _context.Recipes.Include(r => r.RecipeItems).FirstOrDefaultAsync(r => r.RecipeId == id);
        if (recipe is null) return NotFound();

        recipe.ProductId = model.ProductId;
        recipe.Instructions = model.Instructions?.Trim();
        recipe.IsActive = model.IsActive; 
        //Cập nhật các thông tin cơ bản của công thức
        _context.RecipeItems.RemoveRange(recipe.RecipeItems);
        // Xóa toàn bộ RecipeItems cũ của công thức để thay thế bằng danh sách mới
        recipe.RecipeItems = model.Ingredients.Select(i => new RecipeItem
        {
            RecipeId = id,
            IngredientId = i.IngredientId,
            Quantity = i.Quantity
        }).ToList(); //Tạo lại danh sách nguyên liệu mới từ dữ liệu form và gán vào RecipeItems

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã cập nhật công thức thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        // Nhận RecipeId từ form; đọc và cập nhật IsActive trong Recipes.
        var recipe = await _context.Recipes.Include(r => r.Product).FirstOrDefaultAsync(r => r.RecipeId == id);
        if (recipe is null) return NotFound();
        recipe.IsActive = !recipe.IsActive;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã {(recipe.IsActive ? "bật" : "tắt")} công thức của {recipe.Product.Name}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadOptionsAsync(int? includeProductId = null)
    {
        // Dữ liệu dropdown truyền từ DB lên UI qua ViewBag.Products/ViewBag.Ingredients.
        // Query lấy từ bàng Products chỉ lấy các sản phẩm isActive = true và lưu vào ViewBag.Products
        ViewBag.Products = await _context.Products
            .Where(p => p.IsActive &&
                        (p.ProductId == includeProductId ||
                         !_context.Recipes.Any(r => r.ProductId == p.ProductId)))
            .OrderBy(p => p.Name)
            .AsNoTracking()
            .ToListAsync();
        // Query lấy từ bảng Ingredients chỉ lấy các nguyên liệu đang hoạt động và lưu vào ViewBag.Ingre
        ViewBag.Ingredients = await _context.Ingredients.Where(i => i.IsActive).OrderBy(i => i.Name).AsNoTracking().ToListAsync();
    }

    private async Task ValidateRecipeAsync(RecipeEditViewModel model, int? recipeId = null)
    {
        // Nhận dữ liệu từ ViewModel và đối chiếu Product/Ingredient với database trước khi lưu.
        if (!await _context.Products.AnyAsync(p => p.ProductId == model.ProductId && p.IsActive))
            ModelState.AddModelError(nameof(model.ProductId), "Sản phẩm không tồn tại hoặc đã ngừng hoạt động.");

        await ValidateRecipeItemsAsync(model, recipeId);
    }

    private async Task ValidateRecipeItemsAsync(RecipeEditViewModel model, int? recipeId = null)
    {
        if (model.Ingredients.Count == 0)
            ModelState.AddModelError(nameof(model.Ingredients), "Công thức phải có ít nhất một nguyên liệu.");

        if (model.Ingredients.GroupBy(i => i.IngredientId).Any(g => g.Count() > 1))
            ModelState.AddModelError(nameof(model.Ingredients), "Không được chọn trùng nguyên liệu.");

        var ingredientIds = model.Ingredients.Select(i => i.IngredientId).Distinct().ToList();
        var activeCount = await _context.Ingredients.CountAsync(i => ingredientIds.Contains(i.IngredientId) && i.IsActive);
        if (activeCount != ingredientIds.Count)
            ModelState.AddModelError(nameof(model.Ingredients), "Một hoặc nhiều nguyên liệu không tồn tại hoặc đã ngừng hoạt động.");

        var duplicate = await _context.Recipes.AnyAsync(r => r.ProductId == model.ProductId && (!recipeId.HasValue || r.RecipeId != recipeId.Value));
        if (duplicate)
            ModelState.AddModelError(nameof(model.ProductId), "Sản phẩm này đã có công thức.");
    }

    private static void NormalizeIngredients(RecipeEditViewModel model)
    {
        model.Ingredients = model.Ingredients
            .Where(i => i.IngredientId > 0 && i.Quantity > 0)
            .ToList();
    }

    private static RecipeEditViewModel ToViewModel(Recipe recipe) => new()
    {
        RecipeId = recipe.RecipeId,
        ProductId = recipe.ProductId,
        Instructions = recipe.Instructions,
        IsActive = recipe.IsActive,
        Ingredients = recipe.RecipeItems.Select(i => new RecipeIngredientViewModel
        {
            IngredientId = i.IngredientId,
            IngredientName = i.Ingredient.Name,
            Unit = i.Ingredient.Unit,
            Quantity = i.Quantity
        }).ToList()
    };
}
