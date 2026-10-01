using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class InventoryTransaction
{
    public int TxnId { get; set; }

    public int IngredientId { get; set; }

    public string TxnType { get; set; } = null!;

    public decimal QtyChange { get; set; }

    public string? RefTable { get; set; }

    public int? RefId { get; set; }

    public string? Note { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual Ingredient Ingredient { get; set; } = null!;
}
