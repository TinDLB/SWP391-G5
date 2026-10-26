using System.ComponentModel.DataAnnotations;
using SWP391_G5.Models.Entities;

namespace SWP391_G5.Models.ViewModels
{
    public class CheckoutViewModel
    {
        // 1. Địa chỉ giao hàng
        public int? SelectedAddressId { get; set; }

        public bool IsNewAddress { get; set; } = false;

        [Display(Name = "Họ tên người nhận")]
        [StringLength(100, ErrorMessage = "Tên người nhận từ 2 đến 100 ký tự.", MinimumLength = 2)]
        public string? NewReceiverName { get; set; }

        [Display(Name = "Số điện thoại")]
        [RegularExpression(@"^(0[3|5|7|8|9])[0-9]{8}$", ErrorMessage = "Số điện thoại không hợp lệ (10 số).")]
        public string? NewPhone { get; set; }

        [Display(Name = "Địa chỉ nhận hàng")]
        [StringLength(255, ErrorMessage = "Địa chỉ không quá 255 ký tự.")]
        public string? NewAddressLine { get; set; }

        // 2. Phương thức thanh toán (COD | Online)
        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán.")]
        public string PaymentMethod { get; set; } = "COD"; // "COD" hoặc "Online"

        // 3. Khuyến mãi Voucher
        public string? VoucherCode { get; set; }

        // 4. Điểm thưởng thành viên (UC-29 Loyalty Points)
        public bool UsePoints { get; set; } = false;
        public int PointsToUse { get; set; } = 0;
        public int AvailablePoints { get; set; } = 0;
        public decimal PointsDiscountAmount { get; set; } = 0; // Quy đổi: 1 điểm = 1,000 VND

        // 5. Ghi chú đơn hàng
        [StringLength(500, ErrorMessage = "Ghi chú không quá 500 ký tự.")]
        public string? Note { get; set; }

        // 6. Dữ liệu tính toán & hiển thị
        public decimal Subtotal { get; set; }
        public decimal VoucherDiscount { get; set; }
        public decimal TotalAmount { get; set; }
        public int PointsToEarn { get; set; } // Tích lũy: ví dụ 1% giá trị đơn hàng

        public List<CartItemSummaryViewModel> Items { get; set; } = new List<CartItemSummaryViewModel>();
        public List<Address> SavedAddresses { get; set; } = new List<Address>();
    }

    public class CartItemSummaryViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
    }

    public class VoucherCheckResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? VoucherId { get; set; }
        public string? VoucherCode { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal NewTotal { get; set; }
    }
}
