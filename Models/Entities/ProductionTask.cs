using System;
using System.Collections.Generic;

namespace SWP391_G5.Models.Entities;

public partial class ProductionTask
{
    public int TaskId { get; set; }

    public int ProductId { get; set; }

    public int TargetQty { get; set; }

    public int? ActualYield { get; set; }

    public int? DefectQty { get; set; }

    public string Status { get; set; } = null!;

    public int CreatedBy { get; set; }

    public int? AssignedBaker { get; set; }

    public DateTime? Deadline { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? AssignedBakerNavigation { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ProductionTaskIngredient> ProductionTaskIngredients { get; set; } = new List<ProductionTaskIngredient>();
}
