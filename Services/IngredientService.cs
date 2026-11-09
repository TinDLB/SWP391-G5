using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;

namespace SWP391_G5.Services;

public class IngredientService : IIngredientService
{
    private readonly BakeryManagementDbContext _context;

    public IngredientService(BakeryManagementDbContext context) => _context = context;

    // ---------- Danh sách ----------
    public async Task<IngredientListViewModel> GetListAsync(string? keyword, string? filter)
    {
        var f = (filter == "low" || filter == "inactive") ? filter : "active";

        // Bảng nguyên liệu nhỏ (vài chục dòng) nên kéo hết về rồi lọc trong bộ nhớ cho đơn giản
        var all = await _context.Ingredients.AsNoTracking()
            .Select(i => new IngredientItemViewModel
            {
                IngredientId = i.IngredientId,
                Name = i.Name,
                Unit = i.Unit,
                StockQty = i.StockQty,
                MinThreshold = i.MinThreshold,
                IsActive = i.IsActive
            })
            .ToListAsync();

        IEnumerable<IngredientItemViewModel> q = f switch
        {
            "low" => all.Where(i => i.IsLow),
            "inactive" => all.Where(i => !i.IsActive),
            _ => all.Where(i => i.IsActive)
        };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            q = q.Where(i => i.Name.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        return new IngredientListViewModel
        {
            Keyword = keyword,
            Filter = f,
            ActiveCount = all.Count(i => i.IsActive),
            LowCount = all.Count(i => i.IsLow),
            InactiveCount = all.Count(i => !i.IsActive),
            Items = q.OrderByDescending(i => i.IsLow).ThenBy(i => i.Name).ToList()
        };
    }

    // ---------- Chi tiết ----------
    public async Task<IngredientDetailViewModel?> GetDetailAsync(int id)
    {
        var i = await _context.Ingredients.AsNoTracking().FirstOrDefaultAsync(x => x.IngredientId == id);
        if (i == null) return null;

        var usage = await GetUsageAsync(id);

        var txns = await _context.InventoryTransactions.AsNoTracking()
            .Where(t => t.IngredientId == id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .Select(t => new IngredientTxnViewModel
            {
                CreatedAt = t.CreatedAt,
                TxnType = t.TxnType,
                QtyChange = t.QtyChange,
                Note = t.Note
            })
            .ToListAsync();

        return new IngredientDetailViewModel
        {
            IngredientId = i.IngredientId,
            Name = i.Name,
            Unit = i.Unit,
            StockQty = i.StockQty,
            MinThreshold = i.MinThreshold,
            IsActive = i.IsActive,
            UsedInRecipes = usage.Recipes,
            UsedInOpenTasks = usage.OpenTasks,
            RecentTransactions = txns
        };
    }

    // ---------- Lấy dữ liệu cho form Sửa ----------
    public async Task<IngredientFormViewModel?> GetFormAsync(int id)
    {
        var i = await _context.Ingredients.AsNoTracking().FirstOrDefaultAsync(x => x.IngredientId == id);
        if (i == null) return null;

        var usage = await GetUsageAsync(id);

        return new IngredientFormViewModel
        {
            IngredientId = i.IngredientId,
            Name = i.Name,
            Unit = i.Unit,
            MinThreshold = i.MinThreshold,
            StockQty = i.StockQty,
            UnitLocked = usage.HasTxn || usage.Recipes > 0   // quy tắc 3
        };
    }

    // ---------- Thêm ----------
    public async Task<ServiceResult> CreateAsync(IngredientFormViewModel m)
    {
        var name = m.Name.Trim();
        var unit = m.Unit.Trim();

        // Quy tắc 1: tên không trùng (SQL Server so sánh không phân biệt hoa thường)
        if (await _context.Ingredients.AnyAsync(i => i.Name == name))
            return ServiceResult.Fail("Tên nguyên liệu đã tồn tại.");

        _context.Ingredients.Add(new Ingredient
        {
            Name = name,
            Unit = unit,
            MinThreshold = m.MinThreshold,
            StockQty = 0,        // quy tắc 2: tồn kho chỉ tăng qua nhập hàng
            IsActive = true
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail("Không thể lưu nguyên liệu (có thể tên đã tồn tại). Vui lòng thử lại.");
        }

        return ServiceResult.Ok("Đã thêm nguyên liệu mới.");
    }

    // ---------- Sửa ----------
    public async Task<ServiceResult> UpdateAsync(IngredientFormViewModel m)
    {
        var item = await _context.Ingredients.FirstOrDefaultAsync(i => i.IngredientId == m.IngredientId);
        if (item == null) return ServiceResult.Fail("Không tìm thấy nguyên liệu.");

        var name = m.Name.Trim();
        var unit = m.Unit.Trim();

        if (await _context.Ingredients.AnyAsync(i => i.Name == name && i.IngredientId != m.IngredientId))
            return ServiceResult.Fail("Tên nguyên liệu đã tồn tại.");

        // Quy tắc 3: không đổi đơn vị khi đã có giao dịch hoặc công thức
        if (!string.Equals(item.Unit, unit, StringComparison.Ordinal))
        {
            var usage = await GetUsageAsync(item.IngredientId);
            if (usage.HasTxn || usage.Recipes > 0)
                return ServiceResult.Fail("Không thể đổi đơn vị vì nguyên liệu đã có giao dịch kho hoặc đang dùng trong công thức.");
        }

        item.Name = name;
        item.Unit = unit;
        item.MinThreshold = m.MinThreshold;   // KHÔNG đụng StockQty

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail("Không thể lưu thay đổi. Vui lòng thử lại.");
        }

        return ServiceResult.Ok("Đã cập nhật nguyên liệu.");
    }

    // ---------- Ngừng / kích hoạt ----------
    public async Task<ServiceResult> SetActiveAsync(int id, bool isActive)
    {
        var item = await _context.Ingredients.FirstOrDefaultAsync(i => i.IngredientId == id);
        if (item == null) return ServiceResult.Fail("Không tìm thấy nguyên liệu.");

        if (!isActive)
        {
            var usage = await GetUsageAsync(id);
            if (usage.OpenTasks > 0)   // quy tắc 4
                return ServiceResult.Fail($"Nguyên liệu đang nằm trong {usage.OpenTasks} công việc sản xuất chưa hoàn tất nên chưa thể ngừng dùng.");
        }

        item.IsActive = isActive;
        await _context.SaveChangesAsync();
        return ServiceResult.Ok(isActive ? "Đã kích hoạt lại nguyên liệu." : "Đã ngừng dùng nguyên liệu.");
    }

    // ---------- Helper: nguyên liệu này đang được dùng ở đâu? ----------
    private async Task<(int Recipes, int OpenTasks, bool HasTxn)> GetUsageAsync(int id)
    {
        var recipes = await _context.RecipeItems.CountAsync(r => r.IngredientId == id);

        var openTaskIds = _context.ProductionTasks
            .Where(t => t.Status == "Assigned" || t.Status == "InProgress")
            .Select(t => t.TaskId);

        var openTasks = await _context.ProductionTaskIngredients
            .CountAsync(p => p.IngredientId == id && openTaskIds.Contains(p.TaskId));

        var hasTxn = await _context.InventoryTransactions.AnyAsync(t => t.IngredientId == id);

        return (recipes, openTasks, hasTxn);
    }
}