using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class PurchaseOrder
{
    public int PoId { get; set; }

    public string PoCode { get; set; } = null!;

    public int SupplierId { get; set; }

    public int CreatedBy { get; set; }

    public string Status { get; set; } = null!;

    public DateTime OrderDate { get; set; }

    public DateOnly? ExpectedDate { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Note { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<GoodsReceipt> GoodsReceipts { get; set; } = new List<GoodsReceipt>();

    public virtual ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();

    public virtual Supplier Supplier { get; set; } = null!;
}
