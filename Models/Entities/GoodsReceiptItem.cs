using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class GoodsReceiptItem
{
    public int ReceiptItemId { get; set; }

    public int ReceiptId { get; set; }

    public int PoItemId { get; set; }

    public decimal ReceivedQty { get; set; }

    public decimal RejectedQty { get; set; }

    public string? RejectReason { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public virtual PurchaseOrderItem PoItem { get; set; } = null!;

    public virtual GoodsReceipt Receipt { get; set; } = null!;
}
