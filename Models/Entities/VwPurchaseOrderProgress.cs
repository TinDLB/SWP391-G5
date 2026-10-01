using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class VwPurchaseOrderProgress
{
    public int PoId { get; set; }

    public string PoCode { get; set; } = null!;

    public string PoStatus { get; set; } = null!;

    public int PoItemId { get; set; }

    public string IngredientName { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public decimal OrderedQty { get; set; }

    public decimal? AcceptedQty { get; set; }

    public decimal? RemainingQty { get; set; }
}
