using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class Address
{
    public int AddressId { get; set; }

    public int UserId { get; set; }

    public string ReceiverName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string AddressLine { get; set; } = null!;

    public bool IsDefault { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual User User { get; set; } = null!;
}
