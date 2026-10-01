using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class ProductionTaskIngredient
{
    public int TaskId { get; set; }

    public int IngredientId { get; set; }

    public decimal RequiredQty { get; set; }

    public virtual Ingredient Ingredient { get; set; } = null!;

    public virtual ProductionTask Task { get; set; } = null!;
}
