using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class VwLowStockIngredient
{
    public int IngredientId { get; set; }

    public string Name { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public decimal StockQty { get; set; }

    public decimal MinThreshold { get; set; }

    public decimal? ShortageQty { get; set; }
}
