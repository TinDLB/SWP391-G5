namespace SWP391_G5.Models.ViewModels
{
    /// <summary>
    /// Chi tiết 1 task sản xuất dành cho Baker (xem + thực hiện).
    /// </summary>
    public class BakerTaskDetailViewModel
    {
        public int TaskId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int TargetQty { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? Deadline { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Note { get; set; }

        // Nguyên liệu cần dùng cho task này (từ ProductionTaskIngredients)
        public List<TaskIngredientViewModel> Ingredients { get; set; } = new();

        // Dùng khi Baker nhập kết quả hoàn tất
        public int? ActualYield { get; set; }
        public int? DefectQty { get; set; }
    }

    public class TaskIngredientViewModel
    {
        public string IngredientName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal RequiredQty { get; set; }
        public decimal CurrentStock { get; set; }

        /// <summary>True nếu tồn kho hiện tại đủ cho RequiredQty.</summary>
        public bool IsStockSufficient => CurrentStock >= RequiredQty;
    }
}
