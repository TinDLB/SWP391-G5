using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class Invoice
{
    public int InvoiceId { get; set; }

    public int OrderId { get; set; }

    public string InvoiceNo { get; set; } = null!;

    public int? IssuedBy { get; set; }

    public DateTime IssuedAt { get; set; }

    public virtual User? IssuedByNavigation { get; set; }

    public virtual Order Order { get; set; } = null!;
}
