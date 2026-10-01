using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class StockAdjustment
{
    public int AdjustmentId { get; set; }

    public int IngredientId { get; set; }

    public string AdjustType { get; set; } = null!;

    public decimal Quantity { get; set; }

    public string Reason { get; set; } = null!;

    public string? Note { get; set; }

    public int CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Ingredient Ingredient { get; set; } = null!;
}
