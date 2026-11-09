using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;

namespace SWP391_G5.Models.ViewModels;

public class RecipeListItemViewModel
{
    public int RecipeId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int IngredientCount { get; set; }
    public bool IsActive { get; set; }
}

public class RecipeEditViewModel
{
    public int RecipeId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn sản phẩm.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọnn sản phẩm.")]
    public int ProductId { get; set; }

    [StringLength(2000, ErrorMessage = "Hướng dẫn không đượcc viết quá 2000 ký tự.")]
    public string? Instructions { get; set; }

    public bool IsActive { get; set; } = true;

    public List<RecipeIngredientViewModel> Ingredients { get; set; } = new();
}

public class RecipeIngredientViewModel
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.001", "79228162514264337593543950335", ErrorMessage = "Số lượnng phảii lớn hơn 0.")]
    public decimal Quantity { get; set; }
}
