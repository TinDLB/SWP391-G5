using System.ComponentModel.DataAnnotations;
using SWP391_G5.Models.Entities;
namespace SWP391_G5.Models.ViewModels;

public class StockAdjustmentViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nguyên liệu.")]
    public int IngredientId { get; set; }
    [Required, RegularExpression("^(Increase|Decrease)$", ErrorMessage = "Chọn tăng hoặc giảm tồn kho.")]
    public string AdjustType { get; set; } = "Increase";
    [Range(typeof(decimal), "0.001", "999999999999999.999", ErrorMessage = "Số lượng phải lớn hơn 0 và trong giới hạn lưu trữ.")]
    public decimal Quantity { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn lý do.")]
    [RegularExpression("^(Correction|FoundStock|Damaged|Expired|Wastage|Other)$", ErrorMessage = "Lý do không hợp lệ.")]
    public string Reason { get; set; } = "";
    [StringLength(255, ErrorMessage = "Ghi chú tối đa 255 ký tự.")]
    public string? Note { get; set; }
    public List<Ingredient> Ingredients { get; set; } = new();
    public List<StockAdjustment> History { get; set; } = new();
    public static IReadOnlyDictionary<string, string> Reasons { get; } = new Dictionary<string, string>
    {
        ["Correction"] = "Điều chỉnh sai lệch", ["FoundStock"] = "Phát hiện hàng tồn",
        ["Damaged"] = "Hư hỏng", ["Expired"] = "Hết hạn", ["Wastage"] = "Hao hụt", ["Other"] = "Khác"
    };
}
