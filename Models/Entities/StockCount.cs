using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class StockCount
{
    public int CountId { get; set; }

    public DateTime CountDate { get; set; }

    public int CreatedBy { get; set; }

    public string? Note { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<StockCountItem> StockCountItems { get; set; } = new List<StockCountItem>();
}
