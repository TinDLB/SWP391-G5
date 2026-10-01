using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class Order
{
    public int OrderId { get; set; }

    public string OrderCode { get; set; } = null!;

    public string Channel { get; set; } = null!;

    public int? CustomerId { get; set; }

    public int? CashierId { get; set; }

    public int? AddressId { get; set; }

    public int? VoucherId { get; set; }

    public string Status { get; set; } = null!;

    public string PaymentMethod { get; set; } = null!;

    public string PaymentStatus { get; set; } = null!;

    public string? TransactionRef { get; set; }

    public decimal Subtotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? DeliveryStaffName { get; set; }

    public string? DeliveryStaffPhone { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual Address? Address { get; set; }

    public virtual User? Cashier { get; set; }

    public virtual User? Customer { get; set; }

    public virtual Invoice? Invoice { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Voucher? Voucher { get; set; }
}
