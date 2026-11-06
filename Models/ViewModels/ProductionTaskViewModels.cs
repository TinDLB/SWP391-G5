using System.ComponentModel.DataAnnotations;

namespace SWP391_G5.Models.ViewModels;

public class ProductionTaskListItemViewModel
{
    // Data ???c query t? ProductionTasks và truy?n sang b?ng qu?n tr?.
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
    // Các tr??ng này nh?n d? li?u ng??i dùng nh?p t? form t?o production task.
    [Required(ErrorMessage = "Vui lòng ch?n s?n ph?m.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng ch?n s?n ph?m.")]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "S? l??ng c?n s?n xu?t ph?i l?n h?n 0.")]
    public int TargetQty { get; set; }

    [Required(ErrorMessage = "Vui lòng ch?n baker ph? trách.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng ch?n baker ph? trách.")]
    public int? AssignedBaker { get; set; }

    [DataType(DataType.DateTime)]
    public DateTime? Deadline { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú không ???c v??t quá 500 ký t?.")]
    public string? Note { get; set; }
}

public class ProductionIngredientRequirementViewModel
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal RequiredQty { get; set; }
    public decimal StockQty { get; set; }
    public decimal ShortageQty { get; set; }
    public bool IsEnough => ShortageQty <= 0;
}
