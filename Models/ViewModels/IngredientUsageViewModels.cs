namespace SWP391_G5.Models.ViewModels;

public class IngredientUsageReportViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string Period { get; set; } = "day";
    public decimal TotalUsage { get; set; }
    public IReadOnlyList<IngredientUsageItemViewModel> Items { get; set; } = [];
}

public class IngredientUsageItemViewModel
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal UsedQty { get; set; }
    public int TaskCount { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal MinThreshold { get; set; }
    public bool RequiresRestock => CurrentStock <= MinThreshold;
}
