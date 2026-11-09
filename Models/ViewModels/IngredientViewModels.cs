using System.ComponentModel.DataAnnotations;
using SWP391_G5.Models.Entities;
namespace SWP391_G5.Models.ViewModels;
public class IngredientInputModel
{
    [Required(ErrorMessage="Nhập tên nguyên liệu."), StringLength(100)]
    public string Name { get; set; } = "";
    [Required(ErrorMessage="Nhập đơn vị tính."), StringLength(20)]
    public string Unit { get; set; } = "";
    [Range(typeof(decimal), "0", "999999999999999.999", ErrorMessage="Ngưỡng tối thiểu phải không âm và trong giới hạn lưu trữ.")]
    public decimal MinThreshold { get; set; }
}
public class IngredientListViewModel
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public bool LowStock { get; set; }
    public List<Ingredient> Items { get; set; } = new();
}
