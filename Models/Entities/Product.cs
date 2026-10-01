using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class Product
{
    public int ProductId { get; set; }

    public int CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public decimal Price { get; set; }

    public int StockQty { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<ProductionTask> ProductionTasks { get; set; } = new List<ProductionTask>();

    public virtual Recipe? Recipe { get; set; }
}
