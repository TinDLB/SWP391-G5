using System.ComponentModel.DataAnnotations;

namespace SWP391_G5.Models.ViewModels
{
    public class CreatePurchaseOrderViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn nhà cung cấp")]
        public int SupplierId { get; set; }

        [DataType(DataType.Date)]
        public DateOnly? ExpectedDate { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public List<PurchaseOrderItemInputModel> Items { get; set; } = new();
    }

    public class PurchaseOrderItemInputModel
    {
        [Required]
        public int IngredientId { get; set; }

        [Range(0.001, double.MaxValue, ErrorMessage = "Số lượng đặt phải lớn hơn 0")]
        public decimal OrderedQty { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Đơn giá không hợp lệ")]
        public decimal UnitPrice { get; set; }
    }
}