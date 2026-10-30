namespace SWP391_G5.Models.ViewModels
{
    /// <summary>
    /// Form nhập kết quả hoàn tất task sản xuất.
    /// </summary>
    public class CompleteTaskViewModel
    {
        public int TaskId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int TargetQty { get; set; }

        /// <summary>Sản lượng thực tế Baker làm được.</summary>
        public int ActualYield { get; set; }

        /// <summary>Số lượng bánh lỗi/không đạt.</summary>
        public int DefectQty { get; set; }

        public string? Note { get; set; }
    }
}
