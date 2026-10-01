using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class Ingredient
{
    public int IngredientId { get; set; }

    public string Name { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public decimal StockQty { get; set; }

    public decimal MinThreshold { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();

    public virtual ICollection<ProductionTaskIngredient> ProductionTaskIngredients { get; set; } = new List<ProductionTaskIngredient>();

    public virtual ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();

    public virtual ICollection<RecipeItem> RecipeItems { get; set; } = new List<RecipeItem>();

    public virtual ICollection<StockAdjustment> StockAdjustments { get; set; } = new List<StockAdjustment>();

    public virtual ICollection<StockCountItem> StockCountItems { get; set; } = new List<StockCountItem>();
}
