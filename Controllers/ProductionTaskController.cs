using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;

namespace SWP391_G5.Controllers;

[Authorize(Roles = "Admin")]
public class ProductionTaskController : Controller
{
    private readonly BakeryManagementDbContext _context;

    public ProductionTaskController(BakeryManagementDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? status, string? search)
    {
        // Nh?n status/search từ query string v� gi? l?i giá trị ?? truyền về form lọc.
        ViewBag.Status = status;
        ViewBag.Search = search;

        //  Query lấy task từ bảng ProductionTasks, tên sản phẩm từ bảng Products, tên Baker từ User
        var query = _context.ProductionTasks
            .Include(t => t.Product)
            .Include(t => t.AssignedBakerNavigation)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(search) && int.TryParse(search.Trim(), out var taskId))
            query = query.Where(t => t.TaskId == taskId);
        else if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Product.Name.ToLower().Contains(search.Trim().ToLower()));

        // Chuy?n k?t qu? query th�nh ViewModel tr??c khi truy?n d? li?u sang Razor View.
        var tasks = await query.OrderByDescending(t => t.CreatedAt)
            .Select(t => new ProductionTaskListItemViewModel
            {
                TaskId = t.TaskId,
                ProductName = t.Product.Name,
                TargetQty = t.TargetQty,
                Status = t.Status,
                AssignedBakerName = t.AssignedBakerNavigation == null ? null : t.AssignedBakerNavigation.FullName,
                Deadline = t.Deadline,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(); //Chuyển sang ViewModel

        return View(tasks); //trả về kết quả
    }

    public async Task<IActionResult> Create() //Hiển thị form
    {
        // Query c�c Product có Recipe đang hoạt động và danh sách Baker đang hoạt động
        await LoadCreateOptionsAsync();
        return View(new CreateProductionTaskViewModel
        {
            Deadline = DateTime.Now.AddDays(1) //Deadline mặc định được gán là ngày mai
        });
    }

    [HttpGet]
    public async Task<IActionResult> IngredientRequirements(int productId, int targetQty)
    {
        if (productId <= 0 || targetQty <= 0)
            return Json(Array.Empty<ProductionIngredientRequirementViewModel>());

        var recipe = await _context.Recipes
            .Include(r => r.RecipeItems)
                .ThenInclude(item => item.Ingredient)
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.ProductId == productId &&
                r.IsActive &&
                r.YieldQty > 0);

        if (recipe is null)
            return Json(Array.Empty<ProductionIngredientRequirementViewModel>());

        var requirements = recipe.RecipeItems.Select(item =>
        {
            var requiredQty = item.Quantity * targetQty / recipe.YieldQty;
            var shortageQty = Math.Max(0, requiredQty - item.Ingredient.StockQty);

            return new ProductionIngredientRequirementViewModel
            {
                IngredientId = item.IngredientId,
                IngredientName = item.Ingredient.Name,
                Unit = item.Ingredient.Unit,
                RequiredQty = requiredQty,
                StockQty = item.Ingredient.StockQty,
                ShortageQty = shortageQty
            };
        }).ToList();

        return Json(requirements);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProductionTaskViewModel model) //Nhận dữ liệu và tạo task
    {
        // Nhận ProductId, TargetQty, AssignedBaker, Deadline và Note từ form POST.
        model.Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
        await ValidateCreateModelAsync(model);
        if (!ModelState.IsValid)
        {
            await LoadCreateOptionsAsync();
            return View(model);
        }

        // Query lấy recipes và recipeItems
        var recipe = await _context.Recipes
            .Include(r => r.RecipeItems)
            .FirstAsync(r => r.ProductId == model.ProductId && r.IsActive);

        var creatorClaim = User.FindFirstValue(ClaimTypes.NameIdentifier); //Lưu người tạo ra task
        if (!int.TryParse(creatorClaim, out var createdBy))
            return Unauthorized();

        // Tạo entity ProductionTask task từ dữ liệu form 
        var task = new ProductionTask
        {
            ProductId = model.ProductId,
            TargetQty = model.TargetQty,
            Status = "Assigned", //Set trạng thái mới tạo là Asssigned
            CreatedBy = createdBy,
            AssignedBaker = model.AssignedBaker,
            Deadline = model.Deadline,
            Note = model.Note,
            CreatedAt = DateTime.UtcNow
        };

        
        task.ProductionTaskIngredients = recipe.RecipeItems.Select(item => new ProductionTaskIngredient
        {
            IngredientId = item.IngredientId,
            RequiredQty = item.Quantity * model.TargetQty / recipe.YieldQty
        }).ToList(); //Công thức = số nguyên liệu trong công thức x số bánh x sản lượng

        _context.ProductionTasks.Add(task); //Thêm task vào db
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã tạo và giao công việc sản xuất thành công.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateCreateModelAsync(CreateProductionTaskViewModel model)
    {
        // Query Product/Recipe ?? b?o ??m s?n ph?m ???c ch?n c� recipe active tr??c khi t?o task.
        var hasRecipe = await _context.Recipes.AnyAsync(r =>
            r.ProductId == model.ProductId && r.IsActive && r.YieldQty > 0); //Query bảng Recipe với điều kiện RecipeId trùng ProId, isActive = true va YieldQty > 0
        if (!hasRecipe)
            ModelState.AddModelError(nameof(model.ProductId), "Sản phẩm chưa có công thức đang hoạt động.");

        // Query User ?? ch? cho ph�p giao task cho t�i kho?n Baker ?ang ho?t ??ng.
        var validBaker = model.AssignedBaker.HasValue && await _context.Users.AnyAsync(u =>
            u.UserId == model.AssignedBaker.Value && u.Role == "Baker" && u.IsActive); //Query bảng User lấy role = baker, có giá trị id, isActive = true
        if (!validBaker)
            ModelState.AddModelError(nameof(model.AssignedBaker), "Baker không tồn tại hoặc đang bị khóa.");

        if (model.Deadline.HasValue && model.Deadline.Value <= DateTime.Now)
            ModelState.AddModelError(nameof(model.Deadline), "Deadline phải nằm trong tương lai.");
    }

    private async Task LoadCreateOptionsAsync()
    {
        // Query v� truy?n Product c� recipe active c�ng danh s�ch Baker active sang form t?o task.
        ViewBag.Products = await _context.Products
            .Where(p => p.IsActive && p.Recipe != null && p.Recipe.IsActive)
            .OrderBy(p => p.Name)
            .AsNoTracking()
            .ToListAsync();

        ViewBag.Bakers = await _context.Users
            .Where(u => u.Role == "Baker" && u.IsActive)
            .OrderBy(u => u.FullName)
            .AsNoTracking()
            .ToListAsync();
    }
}
