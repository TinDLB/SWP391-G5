using System.ComponentModel.DataAnnotations;

namespace SWP391_G5.Models.ViewModels;

public class ProductionTaskListItemViewModel
{
    public int TaskId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int TargetQty { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AssignedBakerName { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateProductionTaskViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn sản phẩm.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn sản phẩm.")]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng cần sản xuất phải lớn hơn 0.")]
    public int TargetQty { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn baker phụ trách.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn baker phụ trách.")]
    public int? AssignedBaker { get; set; }

    [DataType(DataType.DateTime)]
    public DateTime? Deadline { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú không được viết quá 500 ký tự.")]
    public string? Note { get; set; }
}

public class ProductionIngredientRequirementViewModel
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal RequiredQty { get; set; }
    public decimal AvailableQty { get; set; }
    public decimal StockQty { get; set; }
    public decimal ShortageQty { get; set; }
    public bool IsEnough => ShortageQty <= 0;
}
