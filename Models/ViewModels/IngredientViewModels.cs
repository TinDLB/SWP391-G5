using System.ComponentModel.DataAnnotations;

namespace SWP391_G5.Models.ViewModels
{
    // Một dòng trong danh sách
    public class IngredientItemViewModel
    {
        public int IngredientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal StockQty { get; set; }
        public decimal MinThreshold { get; set; }
        public bool IsActive { get; set; }

        // Thuộc tính tính toán: chỉ cảnh báo, không chặn gì
        public bool IsLow => IsActive && StockQty < MinThreshold;
    }

    // Trang danh sách + bộ lọc
    public class IngredientListViewModel
    {
        public string? Keyword { get; set; }
        public string Filter { get; set; } = "active";   // active | low | inactive
        public int ActiveCount { get; set; }
        public int LowCount { get; set; }
        public int InactiveCount { get; set; }
        public List<IngredientItemViewModel> Items { get; set; } = new();
    }

    // Form Thêm/Sửa dùng chung
    public class IngredientFormViewModel
    {
        public int IngredientId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên nguyên liệu.")]
        [StringLength(100, ErrorMessage = "Tên tối đa 100 ký tự.")]
        [Display(Name = "Tên nguyên liệu")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập đơn vị tính.")]
        [StringLength(20, ErrorMessage = "Đơn vị tối đa 20 ký tự.")]
        [Display(Name = "Đơn vị tính")]
        public string Unit { get; set; } = string.Empty;

        [Range(0, 1000000, ErrorMessage = "Ngưỡng tối thiểu phải từ 0 trở lên.")]
        [Display(Name = "Ngưỡng tồn tối thiểu")]
        public decimal MinThreshold { get; set; }

        // Chỉ để hiển thị khi sửa (không cho sửa)
        public decimal StockQty { get; set; }
        public bool UnitLocked { get; set; }
        public bool IsEdit => IngredientId > 0;
    }

    public class IngredientTxnViewModel
    {
        public DateTime CreatedAt { get; set; }
        public string TxnType { get; set; } = string.Empty;
        public decimal QtyChange { get; set; }
        public string? Note { get; set; }
    }

    public class IngredientDetailViewModel : IngredientItemViewModel
    {
        public int UsedInRecipes { get; set; }
        public int UsedInOpenTasks { get; set; }
        public List<IngredientTxnViewModel> RecentTransactions { get; set; } = new();
    }
}