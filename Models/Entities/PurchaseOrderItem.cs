using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class PurchaseOrderItem
{
    public int PoItemId { get; set; }

    public int PoId { get; set; }

    public int IngredientId { get; set; }

    public decimal OrderedQty { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual ICollection<GoodsReceiptItem> GoodsReceiptItems { get; set; } = new List<GoodsReceiptItem>();

    public virtual Ingredient Ingredient { get; set; } = null!;

    public virtual PurchaseOrder Po { get; set; } = null!;
}
