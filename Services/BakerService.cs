using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;

namespace SWP391_G5.Services
{
    public class BakerService : IBakerService
    {
        private readonly BakeryManagementDbContext _context;

        public BakerService(BakeryManagementDbContext context)
        {
            _context = context;
        }

        // ──────────────────────────────────────────
        // Danh sách task được gán
        // ──────────────────────────────────────────
        public async Task<List<BakerProductionTaskViewModel>> GetAssignedTasksAsync(int bakerId)
        {
            return await _context.ProductionTasks
                .Where(t => t.AssignedBaker == bakerId
                         && t.Status != "Planned"    // Planned chưa gán baker không hiện
                         && t.Status != "Cancelled")
                .Include(t => t.Product)
                .Select(t => new BakerProductionTaskViewModel
                {
                    TaskId    = t.TaskId,
                    ProductName = t.Product.Name,
                    TargetQty = t.TargetQty,
                    Status    = t.Status,
                    Deadline  = t.Deadline
                })
                .OrderBy(t => t.Status == "InProgress" ? 0 :
                              t.Status == "Assigned"   ? 1 : 2)
                .ThenBy(t => t.Deadline)
                .ToListAsync();
        }

        // ──────────────────────────────────────────
        // Chi tiết task + nguyên liệu
        // ──────────────────────────────────────────
        public async Task<BakerTaskDetailViewModel?> GetTaskDetailAsync(int taskId, int bakerId)
        {
            var task = await _context.ProductionTasks
                .AsNoTracking()
                .Include(t => t.Product)
                .Include(t => t.ProductionTaskIngredients)
                    .ThenInclude(ti => ti.Ingredient)
                .FirstOrDefaultAsync(t => t.TaskId == taskId && t.AssignedBaker == bakerId);

            if (task == null) return null;

            var ingredients = task.ProductionTaskIngredients
                .Select(ti => new TaskIngredientViewModel
                {
                    IngredientName = ti.Ingredient.Name,
                    Unit           = ti.Ingredient.Unit,
                    RequiredQty    = ti.RequiredQty,
                    CurrentStock   = ti.Ingredient.StockQty
                })
                .ToList();

            return new BakerTaskDetailViewModel
            {
                TaskId      = task.TaskId,
                ProductName = task.Product.Name,
                TargetQty   = task.TargetQty,
                Status      = task.Status,
                Deadline    = task.Deadline,
                StartedAt   = task.StartedAt,
                CompletedAt = task.CompletedAt,
                Note        = task.Note,
                Ingredients = ingredients,
                ActualYield = task.ActualYield,
                DefectQty   = task.DefectQty
            };
        }

        // ──────────────────────────────────────────
        // Bắt đầu task: Assigned → InProgress
        // ──────────────────────────────────────────
        public async Task<ServiceResult> StartTaskAsync(int taskId, int bakerId)
        {
            var task = await _context.ProductionTasks
                .FirstOrDefaultAsync(t => t.TaskId == taskId && t.AssignedBaker == bakerId);

            if (task == null)
                return ServiceResult.Fail("Không tìm thấy công việc.");

            if (task.Status != "Assigned")
                return ServiceResult.Fail($"Không thể bắt đầu vì trạng thái hiện tại là '{task.Status}'.");

            task.Status    = "InProgress";
            task.StartedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return ServiceResult.Ok("Đã bắt đầu sản xuất.");
        }

        // ──────────────────────────────────────────
        // Hoàn tất task: InProgress → Completed
        // Trừ nguyên liệu + cộng thành phẩm + ghi InventoryTransaction
        // ──────────────────────────────────────────
        public async Task<ServiceResult> CompleteTaskAsync(CompleteTaskViewModel model, int bakerId)
        {
            // --- Validate ---
            if (model.ActualYield < 0)
                return ServiceResult.Fail("Sản lượng thực tế không được âm.");
            if (model.DefectQty < 0)
                return ServiceResult.Fail("Số lượng lỗi không được âm.");
            if (model.DefectQty > model.ActualYield)
                return ServiceResult.Fail("Số lượng lỗi không thể lớn hơn sản lượng thực tế.");

            var task = await _context.ProductionTasks
                .Include(t => t.ProductionTaskIngredients)
                    .ThenInclude(ti => ti.Ingredient)
                .Include(t => t.Product)
                .FirstOrDefaultAsync(t => t.TaskId == model.TaskId && t.AssignedBaker == bakerId);

            if (task == null)
                return ServiceResult.Fail("Không tìm thấy công việc.");

            if (task.Status != "InProgress")
                return ServiceResult.Fail($"Chỉ có thể hoàn tất task đang 'InProgress'. Trạng thái hiện tại: '{task.Status}'.");

            // --- Kiểm tra tồn kho nguyên liệu đủ không ---
            var shortages = task.ProductionTaskIngredients
                .Where(ti => ti.Ingredient.StockQty < ti.RequiredQty)
                .Select(ti => $"{ti.Ingredient.Name}: cần {ti.RequiredQty} {ti.Ingredient.Unit}, còn {ti.Ingredient.StockQty}")
                .ToList();

            if (shortages.Any())
                return ServiceResult.Fail("Tồn kho nguyên liệu không đủ:\n" + string.Join("\n", shortages));

            var now = DateTime.Now;

            // --- Trừ nguyên liệu, ghi InventoryTransaction ---
            foreach (var ti in task.ProductionTaskIngredients)
            {
                ti.Ingredient.StockQty -= ti.RequiredQty;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    IngredientId = ti.IngredientId,
                    TxnType      = "ProductionUse",
                    QtyChange    = -ti.RequiredQty,   // âm = xuất kho
                    RefTable     = "ProductionTasks",
                    RefId        = task.TaskId,
                    Note         = $"Sản xuất: {task.Product.Name} (Task #{task.TaskId})",
                    CreatedBy    = bakerId,
                    CreatedAt    = now
                });
            }

            // --- Cộng thành phẩm vào Products.StockQty ---
            int goodQty = model.ActualYield - model.DefectQty;
            if (goodQty > 0)
            {
                task.Product.StockQty += goodQty;
            }

            // --- Cập nhật task ---
            task.Status      = "Completed";
            task.ActualYield = model.ActualYield;
            task.DefectQty   = model.DefectQty;
            task.CompletedAt = now;
            if (!string.IsNullOrWhiteSpace(model.Note))
                task.Note = model.Note;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return ServiceResult.Fail("Lỗi khi lưu dữ liệu: " + ex.InnerException?.Message);
            }

            return ServiceResult.Ok($"Hoàn tất sản xuất! Thành phẩm đạt: {goodQty}, lỗi: {model.DefectQty}.");
        }
    }
}
