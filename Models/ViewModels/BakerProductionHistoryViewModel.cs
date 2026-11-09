namespace SWP391_G5.Models.ViewModels
{
    public class BakerProductionHistoryItemViewModel
    {
        public int TaskId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int TargetQty { get; set; }
        public int ActualYield { get; set; }
        public int DefectQty { get; set; }
        public int SuccessQty => Math.Max(0, ActualYield - DefectQty);

        public double YieldRate => TargetQty > 0 ? Math.Round((double)SuccessQty * 100 / TargetQty, 1) : 0;

        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Note { get; set; }

        public string DurationText
        {
            get
            {
                if (StartedAt.HasValue && CompletedAt.HasValue)
                {
                    var span = CompletedAt.Value - StartedAt.Value;
                    if (span.TotalHours >= 1)
                        return $"{(int)span.TotalHours}h {span.Minutes}m";
                    return $"{Math.Max(1, (int)span.TotalMinutes)} phút";
                }
                return "-";
            }
        }
    }

    public class BakerProductionHistoryListViewModel
    {
        public string? Keyword { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public int TotalTasks { get; set; }
        public int TotalSuccessQty { get; set; }
        public int TotalDefectQty { get; set; }
        public double OverallRate { get; set; }

        public List<BakerProductionHistoryItemViewModel> Items { get; set; } = new();
    }
}
