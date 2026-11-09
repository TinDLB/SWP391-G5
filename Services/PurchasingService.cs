using System.Data;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;
namespace SWP391_G5.Services;
public class PurchasingService(BakeryManagementDbContext db)
{
    public IQueryable<PurchaseOrder> Orders => db.PurchaseOrders
        .Include(p => p.Supplier).Include(p => p.CreatedByNavigation)
        .Include(p => p.PurchaseOrderItems).ThenInclude(i => i.Ingredient)
        .Include(p => p.PurchaseOrderItems).ThenInclude(i => i.GoodsReceiptItems)
        .Include(p => p.GoodsReceipts).ThenInclude(r => r.ReceivedByNavigation);
    public static decimal Accepted(PurchaseOrderItem i) => i.GoodsReceiptItems.Sum(r => r.ReceivedQty - r.RejectedQty);
    public static bool CanReceive(PurchaseOrder p) => (p.Status is "Sent" or "PartiallyFulfilled") && p.PurchaseOrderItems.Any(i => Accepted(i) < i.OrderedQty);
    public async Task ValidateOrder(CreatePurchaseOrderViewModel m)
    {
        if (!await db.Suppliers.AnyAsync(s => s.SupplierId == m.SupplierId && s.IsActive)) throw new InvalidOperationException("Nhà cung cấp không hoạt động hoặc không tồn tại.");
        if (m.ExpectedDate == null || m.ExpectedDate < DateOnly.FromDateTime(m.OrderDate)) throw new InvalidOperationException("Ngày dự kiến nhận phải từ ngày đặt hàng trở đi.");
        if (m.Items == null || m.Items.Count == 0) throw new InvalidOperationException("Cần ít nhất một nguyên liệu.");
        if (m.Items.Select(i => i.IngredientId).Distinct().Count() != m.Items.Count) throw new InvalidOperationException("Không được chọn trùng nguyên liệu.");
        var ids = m.Items.Select(i => i.IngredientId).ToList();
        if (await db.Ingredients.CountAsync(i => ids.Contains(i.IngredientId) && i.IsActive) != ids.Count) throw new InvalidOperationException("Nguyên liệu không tồn tại hoặc đã ngừng hoạt động.");
        if (m.Items.Any(i => i.OrderedQty <= 0 || i.UnitPrice < 0 || decimal.Round(i.OrderedQty, 3) != i.OrderedQty || decimal.Round(i.UnitPrice, 2) != i.UnitPrice)) throw new InvalidOperationException("Số lượng phải dương (tối đa 3 số lẻ), đơn giá không âm (tối đa 2 số lẻ).");
        if (m.Items.Sum(i => i.OrderedQty * i.UnitPrice) > 9999999999999999.99m) throw new InvalidOperationException("Tổng tiền vượt giới hạn lưu trữ.");
    }
    public async Task<int> SaveOrder(CreatePurchaseOrderViewModel m, int userId, int? id = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await ValidateOrder(m);
        PurchaseOrder p;
        if (id.HasValue)
        {
            p = await Orders.FirstOrDefaultAsync(p => p.PoId == id) ?? throw new InvalidOperationException("Không tìm thấy PO.");
            if (p.Status != "Draft" || p.GoodsReceipts.Count != 0) throw new InvalidOperationException("Chỉ sửa PO nháp chưa có phiếu nhận.");
            db.PurchaseOrderItems.RemoveRange(p.PurchaseOrderItems);
            await db.SaveChangesAsync();
            p.PurchaseOrderItems.Clear();
        }
        else
        {
            var prefix = $"PO-{DateTime.Now:yyyyMMdd}-";
            var sequence = 1;
            string code;
            do { code = prefix + (sequence++).ToString("D3"); } while (await db.PurchaseOrders.AnyAsync(p => p.PoCode == code));
            p = new PurchaseOrder { PoCode = code, CreatedBy = userId, Status = "Draft" };
            db.PurchaseOrders.Add(p);
        }
        p.SupplierId = m.SupplierId; p.OrderDate = m.OrderDate; p.ExpectedDate = m.ExpectedDate; p.Note = m.Note;
        p.TotalAmount = decimal.Round(m.Items.Sum(i => i.OrderedQty * i.UnitPrice), 2, MidpointRounding.AwayFromZero);
        foreach (var i in m.Items) p.PurchaseOrderItems.Add(new PurchaseOrderItem { IngredientId = i.IngredientId, OrderedQty = i.OrderedQty, UnitPrice = i.UnitPrice });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return p.PoId;
    }
    public async Task ChangeStatus(int id, string status)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var p = await Orders.FirstOrDefaultAsync(p => p.PoId == id) ?? throw new InvalidOperationException("Không tìm thấy PO.");
        if (status == "Sent" && p.Status == "Draft" && p.GoodsReceipts.Count == 0)
            await ValidateOrder(new() { SupplierId = p.SupplierId, OrderDate = p.OrderDate, ExpectedDate = p.ExpectedDate, Items = p.PurchaseOrderItems.Select(i => new PurchaseOrderItemInputModel { IngredientId = i.IngredientId, OrderedQty = i.OrderedQty, UnitPrice = i.UnitPrice }).ToList() });
        else if (!(status == "Cancelled" && p.Status is "Draft" or "Sent" or "PartiallyFulfilled"))
            throw new InvalidOperationException("Không thể chuyển trạng thái PO. Đơn hoàn thành/đã hủy được khóa.");
        p.Status = status; await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    public async Task<int> Receive(CreateGoodsReceiptViewModel m, int userId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var p = await Orders.FirstOrDefaultAsync(p => p.PoId == m.PoId) ?? throw new InvalidOperationException("Không tìm thấy PO.");
        if (!CanReceive(p)) throw new InvalidOperationException("PO không ở trạng thái được nhận hàng hoặc đã nhận đủ.");
        if (m.ReceivedDate == null || m.ReceivedDate > DateTime.Now || m.ReceivedDate < p.OrderDate) throw new InvalidOperationException("Ngày nhận phải từ ngày đặt đến hiện tại.");
        if (m.PaymentStatus is not ("Paid" or "Unpaid")) throw new InvalidOperationException("Trạng thái thanh toán không hợp lệ.");
        if (m.Items == null || m.Items.Count == 0 || m.Items.Select(i => i.PoItemId).Distinct().Count() != m.Items.Count) throw new InvalidOperationException("Danh sách nhận hàng trống hoặc trùng dòng.");
        var receipt = new GoodsReceipt { PoId = p.PoId, ReceivedBy = userId, ReceivedDate = m.ReceivedDate.Value, PaymentStatus = m.PaymentStatus, PaidAt = m.PaymentStatus == "Paid" ? DateTime.Now : null, Note = m.Note };
        var accepted = new Dictionary<int, decimal>();
        foreach (var input in m.Items)
        {
            var item = p.PurchaseOrderItems.SingleOrDefault(i => i.PoItemId == input.PoItemId) ?? throw new InvalidOperationException("Dòng nhận hàng không thuộc PO.");
            var qty = input.ReceivedQty - input.RejectedQty;
            var wholeUnit = string.Equals(item.Ingredient.Unit, "quả", StringComparison.OrdinalIgnoreCase);
            var invalidFractionalWholeUnit = wholeUnit && (input.ReceivedQty != decimal.Truncate(input.ReceivedQty) || input.RejectedQty != decimal.Truncate(input.RejectedQty));
            if (input.ReceivedQty < 0 || input.RejectedQty < 0 || qty < 0 || decimal.Round(input.ReceivedQty, 3) != input.ReceivedQty || decimal.Round(input.RejectedQty, 3) != input.RejectedQty || invalidFractionalWholeUnit)
                throw new InvalidOperationException(wholeUnit ? "Vui lòng nhập giá trị hợp lệ. Số lượng tính theo quả phải là số nguyên." : "Số lượng nhận/từ chối không hợp lệ; tối đa 3 số lẻ.");
            if (input.RejectedQty > 0 && string.IsNullOrWhiteSpace(input.RejectReason)) throw new InvalidOperationException($"Nhập lý do từ chối cho {item.Ingredient.Name}.");
            if (qty > item.OrderedQty - Accepted(item)) throw new InvalidOperationException($"{item.Ingredient.Name}: nhận vượt số còn thiếu.");
            if (input.ReceivedQty == 0) continue;
            if (!item.Ingredient.IsActive) throw new InvalidOperationException($"{item.Ingredient.Name} đã ngừng hoạt động.");
            receipt.GoodsReceiptItems.Add(new GoodsReceiptItem { PoItemId = item.PoItemId, ReceivedQty = input.ReceivedQty, RejectedQty = input.RejectedQty, RejectReason = input.RejectReason?.Trim() });
            accepted[item.PoItemId] = qty;
            receipt.TotalCost += qty * item.UnitPrice;
        }
        if (receipt.GoodsReceiptItems.Count == 0) throw new InvalidOperationException("Phải có ít nhất một nguyên liệu được giao.");
        receipt.TotalCost = decimal.Round(receipt.TotalCost, 2, MidpointRounding.AwayFromZero);
        var complete = p.PurchaseOrderItems.All(i => Accepted(i) + accepted.GetValueOrDefault(i.PoItemId) >= i.OrderedQty);
        db.GoodsReceipts.Add(receipt); await db.SaveChangesAsync();
        foreach (var i in receipt.GoodsReceiptItems)
        {
            var qty = i.ReceivedQty - i.RejectedQty;
            if (qty == 0) continue;
            var ingredientId = p.PurchaseOrderItems.Single(x => x.PoItemId == i.PoItemId).IngredientId;
            await db.Ingredients.Where(x => x.IngredientId == ingredientId).ExecuteUpdateAsync(s => s.SetProperty(x => x.StockQty, x => x.StockQty + qty));
            db.InventoryTransactions.Add(new InventoryTransaction { IngredientId = ingredientId, TxnType = "Receipt", QtyChange = qty, RefTable = "GoodsReceipts", RefId = receipt.ReceiptId, CreatedBy = userId, CreatedAt = DateTime.Now, Note = $"Nhập theo {p.PoCode}" });
        }
        p.Status = complete ? "Completed" : "PartiallyFulfilled";
        await db.SaveChangesAsync(); await tx.CommitAsync(); return receipt.ReceiptId;
    }
}

