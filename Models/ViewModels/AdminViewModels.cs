using System.ComponentModel.DataAnnotations;

namespace SWP391_G5.Models.ViewModels;

public class AdminDashboardViewModel
{
    public int ProductCount { get; set; }
    public int TodayOrderCount { get; set; }
    public int ActiveUserCount { get; set; }
    public int LowStockCount { get; set; }
    public decimal TodayRevenue { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int ReportOrderCount { get; set; }
    public int CompletedOrderCount { get; set; }
    public int CancelledOrderCount { get; set; }
    public int NewUserCount { get; set; }
    public decimal ReportRevenue { get; set; }
    public IReadOnlyList<LowStockItemViewModel> LowStockItems { get; set; } = [];
}

public class AdminReportViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int OrderCount { get; set; }
    public int CompletedOrderCount { get; set; }
    public int CancelledOrderCount { get; set; }
    public int NewUserCount { get; set; }
    public decimal Revenue { get; set; }
}

public class LowStockItemViewModel
{
    public int IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal StockQty { get; set; }
    public decimal MinThreshold { get; set; }
}

public class OrderMonitoringViewModel
{
    public string? Status { get; set; }
    public string? Channel { get; set; }
    public string? Search { get; set; }
    public IReadOnlyList<OrderMonitoringItemViewModel> Orders { get; set; } = [];
}

public class OrderMonitoringItemViewModel
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EditProductionTaskViewModel
{
    public int TaskId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn sản phẩm.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn sản phẩm.")]
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng cần sản xuất phải lớn hơn 0.")]
    public int TargetQty { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn baker.")]
    public int? AssignedBaker { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập deadline.")]
    public DateTime? Deadline { get; set; }

    public string? Note { get; set; }
    public string Status { get; set; } = string.Empty;
}
