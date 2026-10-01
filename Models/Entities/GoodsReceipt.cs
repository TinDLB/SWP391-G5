using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class GoodsReceipt
{
    public int ReceiptId { get; set; }

    public int PoId { get; set; }

    public int ReceivedBy { get; set; }

    public DateTime ReceivedDate { get; set; }

    public decimal TotalCost { get; set; }

    public string PaymentStatus { get; set; } = null!;

    public DateTime? PaidAt { get; set; }

    public string? Note { get; set; }

    public virtual ICollection<GoodsReceiptItem> GoodsReceiptItems { get; set; } = new List<GoodsReceiptItem>();

    public virtual PurchaseOrder Po { get; set; } = null!;

    public virtual User ReceivedByNavigation { get; set; } = null!;
}
