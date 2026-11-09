using System.ComponentModel.DataAnnotations;

namespace SWP391_G5.Models.ViewModels;

public class CashierOrderListViewModel
{
    public string? Status { get; set; }
    public string? Search { get; set; }
    public string? Channel { get; set; }
    public IReadOnlyList<CashierOrderListItemViewModel> Orders { get; set; } = [];
}

public class CashierOrderListItemViewModel
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CashierOrderDetailsViewModel
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? ReceiverName { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? DeliveryStaffName { get; set; }
    public string? DeliveryStaffPhone { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? InvoiceNo { get; set; }
    public IReadOnlyList<CashierOrderItemViewModel> Items { get; set; } = [];
}

public class CashierOrderItemViewModel
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount => Quantity * UnitPrice;
}

public class DeliveryAssignmentViewModel
{
    public int OrderId { get; set; }
    [Required, StringLength(100)]
    public string DeliveryStaffName { get; set; } = string.Empty;
    [Required, StringLength(20)]
    public string DeliveryStaffPhone { get; set; } = string.Empty;
}
