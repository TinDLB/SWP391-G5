using SWP391_G5.Models.Entities;

namespace SWP391_G5.Models.ViewModels
{
    public class OrderListViewModel
    {
        public string CurrentStatusFilter { get; set; } = "All";
        public List<OrderSummaryItemViewModel> Orders { get; set; } = new List<OrderSummaryItemViewModel>();
        public int TotalOrderCount { get; set; }
        public int PendingCount { get; set; }
        public int PreparingCount { get; set; }
        public int DeliveringCount { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }
    }

    public class OrderSummaryItemViewModel
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int TotalItems { get; set; }
        public string? FirstItemName { get; set; }
        public string? FirstItemImage { get; set; }
    }

    public class OrderDetailViewModel
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string? TransactionRef { get; set; }

        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public int PointsEarned { get; set; }
        public int PointsUsed { get; set; }

        public string? Note { get; set; }
        public string? VoucherCode { get; set; }

        // Thông tin người nhận
        public string ReceiverName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;

        // Thông tin nhân viên giao hàng (nếu có)
        public string? DeliveryStaffName { get; set; }
        public string? DeliveryStaffPhone { get; set; }

        // Chi tiết danh sách món bánh
        public List<OrderItemDetailViewModel> Items { get; set; } = new List<OrderItemDetailViewModel>();

        // Cho phép hủy nếu đơn còn ở trạng thái Pending
        public bool CanCancel => Status.Equals("Pending", StringComparison.OrdinalIgnoreCase);
    }

    public class OrderItemDetailViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
    }
}
