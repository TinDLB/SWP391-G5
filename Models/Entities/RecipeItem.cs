using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class RecipeItem
{
    public int RecipeId { get; set; }

    public int IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public virtual Ingredient Ingredient { get; set; } = null!;

    public virtual Recipe Recipe { get; set; } = null!;
}
