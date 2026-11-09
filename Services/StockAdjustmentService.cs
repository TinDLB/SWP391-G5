using System.Data;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;
namespace SWP391_G5.Services;

public class StockAdjustmentService(BakeryManagementDbContext db)
{
    public async Task Adjust(StockAdjustmentViewModel model, int userId)
    {
        const decimal max = 999999999999999.999m;
        if (model.AdjustType is not ("Increase" or "Decrease")) throw new InvalidOperationException("Loại điều chỉnh không hợp lệ.");
        if (!StockAdjustmentViewModel.Reasons.ContainsKey(model.Reason ?? "")) throw new InvalidOperationException("Vui lòng chọn lý do hợp lệ.");
        if (model.Quantity <= 0 || model.Quantity > max || decimal.Round(model.Quantity, 3) != model.Quantity)
            throw new InvalidOperationException("Số lượng phải dương và có tối đa 3 chữ số thập phân.");
        var unit = await db.Ingredients.AsNoTracking()
            .Where(i => i.IngredientId == model.IngredientId && i.IsActive)
            .Select(i => i.Unit)
            .SingleOrDefaultAsync();
        if (unit is null) throw new InvalidOperationException("Nguyên liệu không hoạt động hoặc không tồn tại.");
        if (string.Equals(unit.Trim(), "quả", StringComparison.OrdinalIgnoreCase)
            && model.Quantity != decimal.Truncate(model.Quantity))
            throw new InvalidOperationException("Số lượng nguyên liệu tính theo quả phải là số nguyên.");
        var note = model.Note?.Trim();
        if (note?.Length > 255) throw new InvalidOperationException("Ghi chú tối đa 255 ký tự.");
        if (model.Reason == "Other" && string.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("Vui lòng ghi rõ lý do khác.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var delta = model.AdjustType == "Increase" ? model.Quantity : -model.Quantity;
        // Conditional arithmetic update prevents lost updates and negative stock under concurrent requests.
        var affected = await db.Ingredients.Where(i => i.IngredientId == model.IngredientId && i.IsActive
            && i.StockQty >= (delta < 0 ? -delta : 0) && i.StockQty <= (delta > 0 ? max - delta : max))
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.StockQty, i => i.StockQty + delta));
        if (affected != 1) throw new InvalidOperationException("Nguyên liệu không hoạt động, không đủ tồn kho để giảm hoặc tồn kho vượt giới hạn.");
        var now = DateTime.Now;
        var adjustment = new StockAdjustment { IngredientId = model.IngredientId, AdjustType = model.AdjustType,
            Quantity = model.Quantity, Reason = model.Reason!, Note = note, CreatedBy = userId, CreatedAt = now };
        db.StockAdjustments.Add(adjustment);
        await db.SaveChangesAsync();
        db.InventoryTransactions.Add(new InventoryTransaction { IngredientId = model.IngredientId,
            TxnType = delta > 0 ? "AdjustmentIn" : "AdjustmentOut", QtyChange = delta,
            RefTable = "StockAdjustments", RefId = adjustment.AdjustmentId, Note = note ?? StockAdjustmentViewModel.Reasons[model.Reason!],
            CreatedBy = userId, CreatedAt = now });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
