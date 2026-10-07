namespace SWP391_G5.Models.ViewModels
{
    public class BakerProductionTaskViewModel
    {
        public int TaskId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public decimal TargetQty { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime? Deadline { get; set; }
    }
}