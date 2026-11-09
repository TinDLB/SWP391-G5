using System.ComponentModel.DataAnnotations;

namespace SWP391_G5.Models.ViewModels;

public class CashierCounterOrderViewModel
{
    public List<CashierCounterOrderItemViewModel> Items { get; set; } = new();

    [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán.")]
    public string PaymentMethod { get; set; } = "Cash";

    public string? VoucherCode { get; set; }
}

public class CashierCounterOrderItemViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn sản phẩm.")]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0.")]
    public int Quantity { get; set; }
}
