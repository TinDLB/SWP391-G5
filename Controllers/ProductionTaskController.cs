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

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var task = await _context.ProductionTasks.Include(t => t.Product).AsNoTracking()
            .FirstOrDefaultAsync(t => t.TaskId == id);
        if (task == null) return NotFound();
        await LoadBakersAsync();
        await LoadEditProductsAsync();
        return View(new EditProductionTaskViewModel
        {
            TaskId = task.TaskId, ProductId = task.ProductId, ProductName = task.Product.Name, TargetQty = task.TargetQty,
            AssignedBaker = task.AssignedBaker, Deadline = task.Deadline, Note = task.Note, Status = task.Status
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProductionTaskViewModel model)
    {
        // Luồng sửa task: đọc ProductionTasks theo TaskId -> kiểm tra trạng thái và Baker
        // trong Users -> cập nhật người làm, deadline, ghi chú -> tạo Notifications nếu đổi Baker.
        var task = await _context.ProductionTasks.FirstOrDefaultAsync(t => t.TaskId == model.TaskId);
        if (task == null) return NotFound();
        if (task.Status is "Completed" or "Cancelled") ModelState.AddModelError("", "Không thể sửa task đã hoàn tất hoặc đã hủy.");
        var recipe = await _context.Recipes
            .Include(r => r.RecipeItems)
            .FirstOrDefaultAsync(r => r.ProductId == model.ProductId && r.IsActive && r.YieldQty > 0);
        if (recipe == null)
            ModelState.AddModelError(nameof(model.ProductId), "Sản phẩm chưa có công thức đang hoạt động.");
        if (!model.AssignedBaker.HasValue || !await _context.Users.AnyAsync(u => u.UserId == model.AssignedBaker && u.Role == "Baker" && u.IsActive))
            ModelState.AddModelError(nameof(model.AssignedBaker), "Baker không tồn tại hoặc đang bị khóa.");
        if (model.Deadline <= DateTime.Now && task.Status != "InProgress") ModelState.AddModelError(nameof(model.Deadline), "Deadline phải nằm trong tương lai.");
        if (!ModelState.IsValid)
        {
            await LoadBakersAsync();
            await LoadEditProductsAsync();
            return View(model);
        }
        var reassigned = task.AssignedBaker != model.AssignedBaker;
        task.ProductId = model.ProductId;
        task.TargetQty = model.TargetQty;
        task.AssignedBaker = model.AssignedBaker;
        task.Deadline = model.Deadline;
        task.Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
        var oldIngredients = await _context.ProductionTaskIngredients
            .Where(item => item.TaskId == task.TaskId)
            .ToListAsync();
        _context.ProductionTaskIngredients.RemoveRange(oldIngredients);
        var newIngredients = recipe!.RecipeItems.Select(item => new ProductionTaskIngredient
        {
            TaskId = task.TaskId,
            IngredientId = item.IngredientId,
            RequiredQty = item.Quantity * model.TargetQty / recipe.YieldQty
        }).ToList();
        _context.ProductionTaskIngredients.AddRange(newIngredients);
        if (task.Status == "Planned") task.Status = "Assigned";
        if (reassigned) await AddTaskNotificationAsync(task.AssignedBaker!.Value, "Công việc sản xuất mới", $"Bạn được giao task #{task.TaskId}.");
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã cập nhật công việc sản xuất.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        // Luồng hủy: đọc task từ ProductionTasks. Task Planned/Assigned chỉ đổi Status.
        // Nếu task đang InProgress, ngoài cập nhật ProductionTasks.Status còn ghi Notifications
        // để Baker nhận được thông tin hủy ngay trong hệ thống.
        var task = await _context.ProductionTasks
            .Include(t => t.AssignedBakerNavigation)
            .Include(t => t.Product)
            .FirstOrDefaultAsync(t => t.TaskId == id);
        if (task == null) return NotFound();
        if (task.Status is "Completed" or "Cancelled")
            TempData["ErrorMessage"] = "Không thể hủy task đã hoàn tất hoặc đã hủy.";
        else
        {
            var wasInProgress = task.Status == "InProgress";
            task.Status = "Cancelled";

            if (wasInProgress && task.AssignedBaker.HasValue)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = task.AssignedBaker.Value,
                    NotificationType = "System",
                    Title = $"Task #{task.TaskId} đã bị hủy",
                    Message = $"Admin đã hủy công việc sản xuất {task.Product?.Name ?? ""} khi bạn đang thực hiện.",
                    CreatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = wasInProgress
                ? "Đã hủy task và gửi thông báo cho Baker."
                : "Đã hủy task.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadBakersAsync() => ViewBag.Bakers = await _context.Users.Where(u => u.Role == "Baker" && u.IsActive).OrderBy(u => u.FullName).AsNoTracking().ToListAsync();
    private async Task LoadEditProductsAsync() => ViewBag.Products = await _context.Products
        .Where(p => p.IsActive && p.Recipe != null && p.Recipe.IsActive)
        .OrderBy(p => p.Name)
        .AsNoTracking()
        .ToListAsync();
    private Task AddTaskNotificationAsync(int userId, string title, string message)
    {
        _context.Notifications.Add(new Notification { UserId = userId, NotificationType = "TaskAssigned", Title = title, Message = message, CreatedAt = DateTime.Now });
        return Task.CompletedTask;
    }

    public async Task<IActionResult> Index(string? status, string? search)
    {
        // Truy vấn danh sách task từ ProductionTasks, lấy tên sản phẩm từ Products
        // và tên Baker từ Users. Kết quả được chiếu sang ProductionTaskListItemViewModel.
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
        // Đọc Recipes -> RecipeItems -> Ingredients để tính nhu cầu nguyên liệu.
        // Không ghi database; chỉ trả JSON cho giao diện cập nhật preview theo sản phẩm/số lượng.
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
                AvailableQty = Math.Min(requiredQty, item.Ingredient.StockQty),
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
        // Luồng tạo task:
        // 1. Kiểm tra Product có Recipe active và AssignedBaker có Role = Baker trong Users.
        // 2. Đọc công thức từ Recipes/RecipeItems.
        // 3. Ghi ProductionTasks và ProductionTaskIngredients.
        // 4. SaveChanges để database sinh TaskId.
        // 5. Ghi Notifications cho Baker bằng TaskId thật rồi commit transaction.
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
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.SaveChangesAsync();

        _context.Notifications.Add(new Notification
        {
            UserId = model.AssignedBaker!.Value,
            NotificationType = "TaskAssigned",
            Title = "Bạn được giao công việc sản xuất",
            Message = $"Task #{task.TaskId} - {model.TargetQty} sản phẩm.",
            CreatedAt = DateTime.Now
        });
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

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
