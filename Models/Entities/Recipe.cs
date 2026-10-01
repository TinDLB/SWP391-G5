using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class Recipe
{
    public int RecipeId { get; set; }

    public int ProductId { get; set; }

    public int YieldQty { get; set; }

    public string? Instructions { get; set; }

    public bool IsActive { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<RecipeItem> RecipeItems { get; set; } = new List<RecipeItem>();
}
