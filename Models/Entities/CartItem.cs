using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class CartItem
{
    public int UserId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
