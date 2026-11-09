using System.ComponentModel.DataAnnotations;
namespace SWP391_G5.Models.ViewModels;
public class CreatePurchaseOrderViewModel
{
    [Range(1, int.MaxValue)] public int SupplierId { get; set; }
    [Required, DataType(DataType.Date)] public DateTime OrderDate { get; set; } = DateTime.Today;
    [Required, DataType(DataType.Date)] public DateOnly? ExpectedDate { get; set; }
    [StringLength(500)] public string? Note { get; set; }
    public List<PurchaseOrderItemInputModel> Items { get; set; } = new();
}
public class PurchaseOrderItemInputModel
{
    [Range(1, int.MaxValue)] public int IngredientId { get; set; }
    [Range(typeof(decimal), "0.001", "999999999999999.999")] public decimal OrderedQty { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999.99")] public decimal UnitPrice { get; set; }
}
public class CreateGoodsReceiptViewModel
{
    public int PoId { get; set; }
    [Required] public DateTime? ReceivedDate { get; set; } = DateTime.Now;
    [Required, RegularExpression("^(Paid|Unpaid)$")] public string PaymentStatus { get; set; } = "Unpaid";
    [StringLength(500)] public string? Note { get; set; }
    public List<GoodsReceiptInput> Items { get; set; } = new();
}
public class GoodsReceiptInput
{
    public int PoItemId { get; set; }
    [Range(typeof(decimal), "0", "999999999999999.999")] public decimal ReceivedQty { get; set; }
    [Range(typeof(decimal), "0", "999999999999999.999")] public decimal RejectedQty { get; set; }
    [StringLength(255)] public string? RejectReason { get; set; }
}
