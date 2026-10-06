namespace SWP391_G5.Models.ViewModels
{
    public class PendingOrderItemViewModel
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
    }

    public class CashierDashboardViewModel
    {
        public decimal TodayRevenue { get; set; }
        public int TodayCompletedOrders { get; set; }
        public int PendingOnlineOrders { get; set; }
        public int PreparingOnlineOrders { get; set; }
        public int DeliveringOnlineOrders { get; set; }
        public List<PendingOrderItemViewModel> RecentPendingOrders { get; set; } = new();
    }
}