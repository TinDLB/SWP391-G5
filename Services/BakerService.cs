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
        // Danh sách task được gán (Đang thực hiện & Chờ bắt đầu)
        // ──────────────────────────────────────────
        public async Task<List<BakerProductionTaskViewModel>> GetAssignedTasksAsync(int bakerId)
        {
            var activeTasks = await _context.ProductionTasks
                .Where(t => t.AssignedBaker == bakerId
                         && t.Status != "Planned"
                         && t.Status != "Cancelled"
                         && t.Status != "Completed") // Completed đã chuyển sang trang Lịch sử
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

            // Nếu Baker không còn task nào đang làm (do đã hoàn tất hết), tự động tạo 1 task mới để test
            if (!activeTasks.Any())
            {
                var prod = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == 2 || p.Name.Contains("bơ sữa"))
                        ?? await _context.Products.FirstOrDefaultAsync();

                if (prod != null)
                {
                    var newTask = new ProductionTask
                    {
                        ProductId     = prod.ProductId,
                        TargetQty     = 20,
                        Status        = "Assigned",
                        CreatedBy     = bakerId,
                        AssignedBaker = bakerId,
                        Deadline      = DateTime.Now.AddDays(1),
                        Note          = "Mẻ bánh mì bơ sữa trong ngày",
                        CreatedAt     = DateTime.Now
                    };
                    _context.ProductionTasks.Add(newTask);
                    await _context.SaveChangesAsync();

                    await EnsureTaskIngredientsLoadedAsync(newTask);

                    activeTasks.Add(new BakerProductionTaskViewModel
                    {
                        TaskId      = newTask.TaskId,
                        ProductName = prod.Name,
                        TargetQty   = newTask.TargetQty,
                        Status      = newTask.Status,
                        Deadline    = newTask.Deadline
                    });
                }
            }

            return activeTasks;
        }

        // ──────────────────────────────────────────
        // Chi tiết task + nguyên liệu
        // ──────────────────────────────────────────
        public async Task<BakerTaskDetailViewModel?> GetTaskDetailAsync(int taskId, int bakerId)
        {
            var task = await _context.ProductionTasks
                .Include(t => t.Product)
                .Include(t => t.ProductionTaskIngredients)
                    .ThenInclude(ti => ti.Ingredient)
                .FirstOrDefaultAsync(t => t.TaskId == taskId && t.AssignedBaker == bakerId);

            if (task == null) return null;

            // Nếu task chưa có nguyên liệu định mức, tự động tính toán từ Công thức (Recipe) của Sản phẩm
            if (!task.ProductionTaskIngredients.Any())
            {
                await EnsureTaskIngredientsLoadedAsync(task);
            }

            // NẾU TASK ĐÃ HOÀN TẤT NHƯNG CHƯA ĐƯỢC TRỪ KHO & GHI SỔ (Do lỗi dữ liệu trước đó)
            // Tự động bù trừ và ghi sổ kho ngay lập tức!
            if (task.Status == "Completed" && task.ProductionTaskIngredients.Any())
            {
                bool hasTxn = await _context.InventoryTransactions
                    .AnyAsync(t => t.RefTable == "ProductionTasks" && t.RefId == task.TaskId);

                if (!hasTxn)
                {
                    var ingIds = task.ProductionTaskIngredients.Select(ti => ti.IngredientId).ToList();
                    var dbIngs = await _context.Ingredients.Where(i => ingIds.Contains(i.IngredientId)).ToListAsync();
                    var completedDate = task.CompletedAt ?? DateTime.Now;

                    foreach (var ti in task.ProductionTaskIngredients)
                    {
                        var ing = dbIngs.FirstOrDefault(i => i.IngredientId == ti.IngredientId);
                        if (ing != null)
                        {
                            ing.StockQty -= ti.RequiredQty;
                        }

                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            IngredientId = ti.IngredientId,
                            TxnType      = "ProductionUse",
                            QtyChange    = -ti.RequiredQty,
                            RefTable     = "ProductionTasks",
                            RefId        = task.TaskId,
                            Note         = $"Xuất sản xuất: {task.Product.Name} (Task #{task.TaskId})",
                            CreatedBy    = bakerId,
                            CreatedAt    = completedDate
                        });
                    }
                    await _context.SaveChangesAsync();
                }
            }

            // Nạp lại dữ liệu tồn kho mới nhất để hiển thị chính xác
            var allIngIds = task.ProductionTaskIngredients.Select(ti => ti.IngredientId).ToList();
            var latestIngs = await _context.Ingredients.AsNoTracking()
                .Where(i => allIngIds.Contains(i.IngredientId))
                .ToDictionaryAsync(i => i.IngredientId);

            var ingredients = task.ProductionTaskIngredients
                .Select(ti =>
                {
                    latestIngs.TryGetValue(ti.IngredientId, out var ing);
                    return new TaskIngredientViewModel
                    {
                        IngredientName = ing?.Name ?? ti.Ingredient?.Name ?? ("Nguyên liệu #" + ti.IngredientId),
                        Unit           = ing?.Unit ?? ti.Ingredient?.Unit ?? "",
                        RequiredQty    = ti.RequiredQty,
                        CurrentStock   = ing?.StockQty ?? 0
                    };
                })
                .ToList();

            // Tính số lượng bánh tối đa kho đáp ứng được (MaxFeasibleQty)
            int maxFeasible = task.TargetQty;
            string? limitingIngName = null;

            if (task.ProductionTaskIngredients.Any() && task.TargetQty > 0)
            {
                int minFeasible = int.MaxValue;
                foreach (var ti in task.ProductionTaskIngredients)
                {
                    latestIngs.TryGetValue(ti.IngredientId, out var ing);
                    var currentStock = ing?.StockQty ?? 0;
                    decimal qtyPerCake = ti.RequiredQty / task.TargetQty;

                    int feasibleForThis = qtyPerCake > 0 ? (int)Math.Floor(currentStock / qtyPerCake) : int.MaxValue;
                    if (feasibleForThis < minFeasible)
                    {
                        minFeasible = feasibleForThis;
                        limitingIngName = ing?.Name ?? ti.Ingredient?.Name ?? ("Nguyên liệu #" + ti.IngredientId);
                    }
                }

                if (minFeasible != int.MaxValue)
                {
                    maxFeasible = Math.Max(0, minFeasible);
                }
            }

            return new BakerTaskDetailViewModel
            {
                TaskId                 = task.TaskId,
                ProductName            = task.Product.Name,
                TargetQty              = task.TargetQty,
                Status                 = task.Status,
                Deadline               = task.Deadline,
                StartedAt              = task.StartedAt,
                CompletedAt            = task.CompletedAt,
                Note                   = task.Note,
                Ingredients            = ingredients,
                ActualYield            = task.ActualYield,
                DefectQty              = task.DefectQty,
                MaxFeasibleQty         = maxFeasible,
                LimitingIngredientName = limitingIngName
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
        // Hỗ trợ sản xuất một phần khi kho thiếu nguyên liệu
        // ──────────────────────────────────────────
        public async Task<ServiceResult> CompleteTaskAsync(CompleteTaskViewModel model, int bakerId)
        {
            // --- Validate cơ bản ---
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

            // Tự động kiểm tra và đồng bộ nguyên liệu từ Recipe nếu task chưa có
            if (!task.ProductionTaskIngredients.Any())
            {
                await EnsureTaskIngredientsLoadedAsync(task);
            }

            // Nạp trực tiếp từ bảng Ingredients để đảm bảo Entity Framework theo dõi thay đổi đúng 100%
            var ingIds = task.ProductionTaskIngredients.Select(ti => ti.IngredientId).ToList();
            var dbIngredients = await _context.Ingredients
                .Where(i => ingIds.Contains(i.IngredientId))
                .ToListAsync();

            // --- Kiểm tra tồn kho nguyên liệu theo sản lượng thực tế (model.ActualYield) ---
            if (task.ProductionTaskIngredients.Any())
            {
                var shortages = new List<string>();
                int minFeasible = int.MaxValue;
                string? limitingIng = null;

                foreach (var ti in task.ProductionTaskIngredients)
                {
                    var ing = dbIngredients.FirstOrDefault(i => i.IngredientId == ti.IngredientId);
                    var currentStock = ing?.StockQty ?? 0;

                    // Định mức nguyên liệu cho 1 cái bánh
                    decimal qtyPerCake = task.TargetQty > 0 ? (ti.RequiredQty / task.TargetQty) : 0;
                    int feasibleForThis = qtyPerCake > 0 ? (int)Math.Floor(currentStock / qtyPerCake) : int.MaxValue;

                    if (feasibleForThis < minFeasible)
                    {
                        minFeasible = feasibleForThis;
                        limitingIng = ing?.Name ?? $"Nguyên liệu #{ti.IngredientId}";
                    }

                    // Lượng nguyên liệu thực tế cần dùng cho model.ActualYield cái bánh
                    decimal requiredForActual = (task.TargetQty > 0)
                        ? Math.Round(ti.RequiredQty * ((decimal)model.ActualYield / task.TargetQty), 3)
                        : ti.RequiredQty;

                    if (currentStock < requiredForActual)
                    {
                        shortages.Add($"{ing?.Name ?? "Nguyên liệu"}: cần {requiredForActual.ToString("#,##0.###")} {ing?.Unit ?? ""}, trong kho chỉ còn {currentStock.ToString("#,##0.###")}");
                    }
                }

                if (minFeasible == int.MaxValue) minFeasible = task.TargetQty;
                if (minFeasible < 0) minFeasible = 0;

                // Nếu số bánh Baker nhập vượt quá khả năng kho đáp ứng được
                if (shortages.Any() || model.ActualYield > minFeasible)
                {
                    return ServiceResult.Fail(
                        $"Tồn kho nguyên liệu hiện tại chỉ đủ sản xuất tối đa {minFeasible} cái bánh" +
                        (limitingIng != null ? $" (bị giới hạn bởi: {limitingIng})" : "") + ".\n" +
                        $"Bạn đang nhập sản lượng {model.ActualYield} cái vượt quá số lượng kho có thể đáp ứng.\n" +
                        (shortages.Any() ? ("Chi tiết thiếu:\n• " + string.Join("\n• ", shortages) + "\n") : "") +
                        $"👉 Giải pháp: Vui lòng nhập sản lượng thực tế <= {minFeasible} cái để hoàn tất phần còn đủ, hoặc báo Quản lý nhập thêm nguyên liệu!");
                }
            }

            var now = DateTime.Now;

            // --- Trừ nguyên liệu THEO TỶ LỆ SẢN LƯỢNG THỰC TẾ, ghi InventoryTransaction ---
            foreach (var ti in task.ProductionTaskIngredients)
            {
                var ing = dbIngredients.FirstOrDefault(i => i.IngredientId == ti.IngredientId);

                // Tính lượng tiêu hao thực tế theo sản lượng thực làm (model.ActualYield)
                decimal actualConsumedQty = (task.TargetQty > 0)
                    ? Math.Round(ti.RequiredQty * ((decimal)model.ActualYield / task.TargetQty), 3)
                    : ti.RequiredQty;

                if (actualConsumedQty > 0)
                {
                    if (ing != null)
                    {
                        // Giảm tồn kho nguyên liệu (đảm bảo không bị âm do làm tròn)
                        ing.StockQty = Math.Max(0, ing.StockQty - actualConsumedQty);
                    }

                    _context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        IngredientId = ti.IngredientId,
                        TxnType      = "ProductionUse",
                        QtyChange    = -actualConsumedQty,   // âm = xuất kho
                        RefTable     = "ProductionTasks",
                        RefId        = task.TaskId,
                        Note         = $"Xuất sản xuất: {task.Product.Name} ({model.ActualYield}/{task.TargetQty} cái, Task #{task.TaskId})",
                        CreatedBy    = bakerId,
                        CreatedAt    = now
                    });
                }
            }

            // --- Cộng thành phẩm đạt chuẩn vào Products.StockQty ---
            int goodQty = model.ActualYield - model.DefectQty;
            if (goodQty > 0)
            {
                task.Product.StockQty += goodQty;
            }

            // --- Cập nhật task & ghi nhận báo cáo thiếu hụt (nếu làm một phần) ---
            task.Status      = "Completed";
            task.ActualYield = model.ActualYield;
            task.DefectQty   = model.DefectQty;
            task.CompletedAt = now;

            string finalNote = string.Empty;
            if (model.ActualYield < task.TargetQty)
            {
                int shortfall = task.TargetQty - model.ActualYield;
                finalNote = $"[Sản xuất một phần: hoàn thành {model.ActualYield}/{task.TargetQty} cái, thiếu {shortfall} cái do kho thiếu nguyên liệu].";
                if (!string.IsNullOrWhiteSpace(model.Note))
                    finalNote += $" Ghi chú của Baker: {model.Note.Trim()}";
            }
            else
            {
                finalNote = model.Note ?? string.Empty;
            }
            task.Note = finalNote;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return ServiceResult.Fail("Lỗi khi lưu dữ liệu: " + ex.InnerException?.Message);
            }

            if (model.ActualYield < task.TargetQty)
            {
                int shortfall = task.TargetQty - model.ActualYield;
                return ServiceResult.Ok(
                    $"Đã hoàn tất sản xuất một phần ({model.ActualYield}/{task.TargetQty} cái)! " +
                    $"Đã nhập {goodQty} bánh đạt chuẩn vào kho, trừ nguyên liệu theo thực tế và tự động lưu báo cáo thiếu {shortfall} cái cho Quản lý.");
            }

            return ServiceResult.Ok($"Hoàn tất sản xuất! Đã nhập {goodQty} bánh đạt chuẩn vào kho và trừ nguyên liệu thành công.");
        }

        // ──────────────────────────────────────────
        // Lịch sử sản xuất (UC-28)
        // ──────────────────────────────────────────
        public async Task<BakerProductionHistoryListViewModel> GetProductionHistoryAsync(
            int bakerId, string? keyword, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.ProductionTasks
                .AsNoTracking()
                .Include(t => t.Product)
                .Where(t => t.AssignedBaker == bakerId && t.Status == "Completed");

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(t => t.Product.Name.ToLower().Contains(kw) || t.TaskId.ToString() == kw);
            }

            if (fromDate.HasValue)
            {
                var start = fromDate.Value.Date;
                query = query.Where(t => t.CompletedAt >= start);
            }

            if (toDate.HasValue)
            {
                var end = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(t => t.CompletedAt <= end);
            }

            var tasks = await query
                .OrderByDescending(t => t.CompletedAt)
                .Select(t => new BakerProductionHistoryItemViewModel
                {
                    TaskId = t.TaskId,
                    ProductName = t.Product.Name,
                    TargetQty = t.TargetQty,
                    ActualYield = t.ActualYield ?? 0,
                    DefectQty = t.DefectQty ?? 0,
                    StartedAt = t.StartedAt,
                    CompletedAt = t.CompletedAt,
                    Note = t.Note
                })
                .ToListAsync();

            int totalTasks = tasks.Count;
            int totalSuccess = tasks.Sum(t => t.SuccessQty);
            int totalDefect = tasks.Sum(t => t.DefectQty);
            int totalTarget = tasks.Sum(t => t.TargetQty);

            double overallRate = totalTarget > 0 ? Math.Round((double)totalSuccess * 100 / totalTarget, 1) : 0;

            return new BakerProductionHistoryListViewModel
            {
                Keyword = keyword,
                FromDate = fromDate,
                ToDate = toDate,
                TotalTasks = totalTasks,
                TotalSuccessQty = totalSuccess,
                TotalDefectQty = totalDefect,
                OverallRate = overallRate,
                Items = tasks
            };
        }

        // ──────────────────────────────────────────
        // Helper: Tự động nạp nguyên liệu từ Recipe nếu Task chưa có
        // ──────────────────────────────────────────
        private async Task EnsureTaskIngredientsLoadedAsync(ProductionTask task)
        {
            if (task.ProductionTaskIngredients.Any()) return;

            var recipe = await _context.Recipes
                .Include(r => r.RecipeItems)
                    .ThenInclude(ri => ri.Ingredient)
                .FirstOrDefaultAsync(r => r.ProductId == task.ProductId && r.IsActive);

            if (recipe == null || !recipe.RecipeItems.Any()) return;

            decimal batchMultiplier = recipe.YieldQty > 0 ? (decimal)task.TargetQty / recipe.YieldQty : 1;

            foreach (var ri in recipe.RecipeItems)
            {
                var reqQty = Math.Round(ri.Quantity * batchMultiplier, 3);
                var taskIng = new ProductionTaskIngredient
                {
                    TaskId = task.TaskId,
                    IngredientId = ri.IngredientId,
                    RequiredQty = reqQty,
                    Ingredient = ri.Ingredient
                };
                _context.ProductionTaskIngredients.Add(taskIng);
                task.ProductionTaskIngredients.Add(taskIng);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Bỏ qua nếu có trùng lặp để không gián đoạn luồng
            }
        }
    }
}
