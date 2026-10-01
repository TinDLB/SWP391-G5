using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class StockCountItem
{
    public int CountItemId { get; set; }

    public int CountId { get; set; }

    public int IngredientId { get; set; }

    public decimal SystemQty { get; set; }

    public decimal CountedQty { get; set; }

    public decimal? Variance { get; set; }

    public virtual StockCount Count { get; set; } = null!;

    public virtual Ingredient Ingredient { get; set; } = null!;
}
