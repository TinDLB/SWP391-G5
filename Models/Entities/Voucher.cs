using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class Voucher
{
    public int VoucherId { get; set; }

    public string Code { get; set; } = null!;

    public string DiscountType { get; set; } = null!;

    public decimal DiscountValue { get; set; }

    public decimal MinOrderValue { get; set; }

    public decimal? MaxDiscount { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public int? UsageLimit { get; set; }

    public int UsedCount { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
