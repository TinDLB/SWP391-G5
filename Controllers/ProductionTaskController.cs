using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;
using SWP391_G5.Services;

namespace SWP391_G5.Controllers;

[Authorize(Roles = "Admin")]
public class ProductionTaskController : Controller
{
    private readonly BakeryManagementDbContext _context;
    private readonly INotificationService _notificationService;

    public ProductionTaskController(
        BakeryManagementDbContext context,
        INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        // [UI -> Controller] Nút Edit truyền TaskId qua route: /ProductionTask/Edit/{id}.
        // [Database - đọc] ProductionTasks lấy task cần sửa; Products lấy tên sản phẩm.
        // LoadBakersAsync đọc Users để tạo danh sách Baker đang hoạt động cho dropdown.
        // LoadEditProductsAsync đọc Products và Recipes để chỉ hiển thị sản phẩm có công thức active.
        // [Controller -> UI] Ghép dữ liệu thành EditProductionTaskViewModel và ViewBag,
        // rồi View(...) truyền model/options sang Views/ProductionTask/Edit.cshtml.
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
        // [UI -> Controller] Form Edit POST gửi TaskId, ProductId, TargetQty, AssignedBaker,
        // Deadline và Note; model binding nhận các giá trị vào EditProductionTaskViewModel.
        // [Database - đọc/kiểm tra] Đọc ProductionTasks để lấy task hiện tại; đọc Recipes/
        // RecipeItems để lấy công thức mới; đọc Users để kiểm tra Baker còn active.
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
        var previousBakerId = task.AssignedBaker;
        var reassigned = task.AssignedBaker != model.AssignedBaker;
        task.ProductId = model.ProductId;
        task.TargetQty = model.TargetQty;
        task.AssignedBaker = model.AssignedBaker;
        task.Deadline = model.Deadline;
        task.Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
        var oldIngredients = await _context.ProductionTaskIngredients
            .Where(item => item.TaskId == task.TaskId)
            .ToListAsync();
        // [Database - xóa/ghi] Xóa các dòng nguyên liệu cũ trong ProductionTaskIngredients,
        // tính lại RequiredQty từ RecipeItems rồi thêm các dòng mới vào bảng này.
        _context.ProductionTaskIngredients.RemoveRange(oldIngredients);
        var newIngredients = recipe!.RecipeItems.Select(item => new ProductionTaskIngredient
        {
            TaskId = task.TaskId,
            IngredientId = item.IngredientId,
            RequiredQty = item.Quantity * model.TargetQty / recipe.YieldQty
        }).ToList();
        _context.ProductionTaskIngredients.AddRange(newIngredients);
        if (task.Status == "Planned") task.Status = "Assigned";
        await _context.SaveChangesAsync();
        if (task.AssignedBaker.HasValue)
        {
            // Thông báo cho Baker sau khi UPDATE thành công, áp dụng cả khi chỉ sửa sản phẩm,
            // số lượng, deadline hoặc ghi chú; không chỉ khi đổi người nhận task.
            await _notificationService.CreateNotificationAsync(
                task.AssignedBaker.Value,
                reassigned ? "TaskReassigned" : "TaskUpdated",
                reassigned ? "Bạn được giao lại công việc sản xuất" : "Công việc sản xuất đã được cập nhật",
                $"Task #{task.TaskId} đã được cập nhật bởi Admin.");
        }
        if (reassigned && previousBakerId.HasValue && previousBakerId != task.AssignedBaker)
        {
            await _notificationService.CreateNotificationAsync(
                previousBakerId.Value,
                "TaskReassigned",
                "Công việc sản xuất đã được phân công lại",
                $"Task #{task.TaskId} đã được giao cho Baker khác.");
        }
        if (reassigned)
        {
            await _notificationService.NotifyAdminsAsync(
                "TaskReassigned",
                "Đã phân công lại task sản xuất",
                $"Task #{task.TaskId} đã được giao cho Baker mới.");
        }
        // [Database - lưu] SaveChanges ghi UPDATE ProductionTasks, xóa/thêm
        // ProductionTaskIngredients và INSERT Notifications nếu có đổi Baker.
        // [Controller -> UI] TempData chứa thông báo một lần, RedirectToAction(Index)
        // chuyển trình duyệt về danh sách task để query và hiển thị lại dữ liệu mới.
        TempData["SuccessMessage"] = "Đã cập nhật công việc sản xuất.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        // [UI -> Controller] Form hủy gửi TaskId qua route/form.
        // [Database - đọc] Query ProductionTasks theo TaskId, đồng thời đọc Users qua
        // AssignedBakerNavigation và Products để lấy người nhận/sản phẩm trong thông báo.
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

            var assignedBakerId = task.AssignedBaker;
            await _context.SaveChangesAsync();
            if (assignedBakerId.HasValue)
            {
                // Gửi thông báo cho Baker ở mọi trạng thái có thể hủy, không chỉ InProgress.
                await _notificationService.CreateNotificationAsync(
                    assignedBakerId.Value,
                    "TaskCancelled",
                    "Công việc sản xuất đã bị hủy",
                    $"Task #{task.TaskId} của sản phẩm {task.Product?.Name ?? ""} đã bị Admin hủy.");
            }
            await _notificationService.NotifyAdminsAsync(
                "TaskCancelled",
                "Task sản xuất đã bị hủy",
                $"Task #{task.TaskId} của sản phẩm {task.Product?.Name ?? ""} đã bị hủy.");
            // [Database - lưu] SaveChanges UPDATE ProductionTasks.Status = Cancelled
            // và INSERT Notifications (nếu task đang InProgress).
            // [Controller -> UI] Redirect về Index; TempData hiển thị kết quả trên danh sách.
            TempData["SuccessMessage"] = wasInProgress
                ? "Đã hủy task và gửi thông báo cho Baker."
                : "Đã hủy task.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadBakersAsync()
    {
        // [Database - đọc] Users là nguồn danh sách Baker active.
        // [Controller -> UI] ViewBag.Bakers truyền danh sách này vào dropdown của form.
        ViewBag.Bakers = await _context.Users.Where(u => u.Role == "Baker" && u.IsActive).OrderBy(u => u.FullName).AsNoTracking().ToListAsync();
    }
    private async Task LoadEditProductsAsync()
    {
        // [Database - đọc] Products kết hợp Recipes để lấy sản phẩm đang bán và có recipe active.
        // [Controller -> UI] ViewBag.Products truyền kết quả vào dropdown chọn sản phẩm.
        ViewBag.Products = await _context.Products
            .Where(p => p.IsActive && p.Recipe != null && p.Recipe.IsActive)
            .OrderBy(p => p.Name)
            .AsNoTracking()
            .ToListAsync();
    }
    private Task AddTaskNotificationAsync(int userId, string title, string message)
    {
        // [Database - ghi chờ lưu] Tạo entity Notification trong DbContext; dữ liệu thực sự
        // được INSERT vào Notifications khi action gọi SaveChangesAsync().
        _context.Notifications.Add(new Notification { UserId = userId, NotificationType = "TaskAssigned", Title = title, Message = message, CreatedAt = DateTime.Now });
        return Task.CompletedTask;
    }

    public async Task<IActionResult> Index(string? status, string? search)
    {
        // [UI -> Controller] Bộ lọc gửi status/search qua query string.
        // [Database - đọc] ProductionTasks là bảng chính; Products cung cấp ProductName,
        // Users cung cấp AssignedBakerName. Điều kiện Where lọc theo trạng thái/TaskId/tên bánh.
        // [Controller -> UI] Select map kết quả sang ProductionTaskListItemViewModel,
        // View(tasks) truyền danh sách sang Views/ProductionTask/Index.cshtml.
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
        // [Database - đọc] LoadCreateOptionsAsync query Products có Recipes active và Users có
        // Role = Baker, IsActive = true; [Controller -> UI] truyền options qua ViewBag cho form.
        await LoadCreateOptionsAsync();
        return View(new CreateProductionTaskViewModel
        {
            Deadline = DateTime.Now.AddDays(1) //Deadline mặc định được gán là ngày mai
        });
    }

    [HttpGet]
    public async Task<IActionResult> IngredientRequirements(int productId, int targetQty)
    {
        // [UI -> Controller] JavaScript gọi GET AJAX với productId và targetQty khi người dùng
        // đổi sản phẩm/số lượng. [Database - đọc] Recipes lấy công thức, RecipeItems lấy định
        // lượng, Ingredients lấy tên/đơn vị/tồn kho để tính thiếu hụt.
        // [Controller -> UI] Không lưu DB; trả JSON ProductionIngredientRequirementViewModel
        // để JavaScript render phần preview nguyên liệu trên form.
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
        // [UI -> Controller] Form POST gửi ProductId, TargetQty, AssignedBaker, Deadline, Note.
        // [Database - đọc] ValidateCreateModelAsync kiểm tra Recipes và Users; query tiếp
        // Recipes/RecipeItems để lấy nguyên liệu và công thức tính RequiredQty.
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
                .ThenInclude(item => item.Ingredient)
            .FirstAsync(r => r.ProductId == model.ProductId && r.IsActive && r.YieldQty > 0);

        var shortageItems = recipe.RecipeItems
            .Select(item => new
            {
                item.Ingredient.Name,
                item.Ingredient.Unit,
                RequiredQty = item.Quantity * model.TargetQty / recipe.YieldQty,
                StockQty = item.Ingredient.StockQty
            })
            .Where(item => item.RequiredQty > item.StockQty)
            .ToList();

        var shortageMessage = shortageItems.Count == 0
            ? null
            : $"Task sản xuất sản phẩm #{model.ProductId} thiếu nguyên liệu: " +
              string.Join("; ", shortageItems.Select(item =>
                  $"{item.Name} thiếu {(item.RequiredQty - item.StockQty).ToString("#,##0.###")} {item.Unit}"));

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

        // [Database - ghi chờ lưu] Add task và các ProductionTaskIngredients vào DbContext.
        // Chưa ghi thật xuống DB cho đến khi SaveChangesAsync được gọi.
        _context.ProductionTasks.Add(task); //Thêm task vào db
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.SaveChangesAsync();
        // [Database - lưu lần 1] INSERT ProductionTasks và ProductionTaskIngredients;
        // database sinh TaskId để dùng trong nội dung Notification.

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
        // Notification cho Baker có UserId của Baker; đồng thời tạo notification cho Admin
        // để kiểm tra được việc giao task trong /Admin/Notifications.
        await _notificationService.NotifyAdminsAsync(
            "TaskAssigned",
            "Đã giao task sản xuất",
            $"Task #{task.TaskId} đã được giao cho Baker.");
        if (shortageMessage is not null)
        {
            // Task vẫn được tạo, nhưng ghi thông báo cho Admin và Baker để xử lý bổ sung kho.
            await _notificationService.NotifyAdminsAsync(
                "ProductionIngredientShortage",
                "Task sản xuất thiếu nguyên liệu",
                $"Task #{task.TaskId}: {shortageMessage}");
            await _notificationService.CreateNotificationAsync(
                model.AssignedBaker!.Value,
                "ProductionIngredientShortage",
                "Task sản xuất đang thiếu nguyên liệu",
                $"Task #{task.TaskId}: {shortageMessage}");
        }
        // [Database - lưu lần 2] INSERT Notifications cho Baker, sau đó commit transaction.
        // [Controller -> UI] TempData truyền thông báo một lần và redirect về Index;
        // Index query lại ProductionTasks rồi render task mới lên UI.

        TempData["SuccessMessage"] = "Đã tạo và giao công việc sản xuất thành công.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateCreateModelAsync(CreateProductionTaskViewModel model)
    {
        // [Database - chỉ đọc] Recipes xác nhận ProductId có công thức active;
        // Users xác nhận AssignedBaker tồn tại, đúng role Baker và đang active.
        // Method chỉ thêm lỗi vào ModelState, không ghi dữ liệu xuống DB.
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
        // [Database - đọc] Query Products/Recipes để lấy sản phẩm có recipe active và Users
        // để lấy Baker active; [Controller -> UI] truyền hai danh sách qua ViewBag cho form Create.
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
