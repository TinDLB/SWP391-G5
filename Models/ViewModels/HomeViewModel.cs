using SWP391_G5.Models.Entities;

namespace SWP391_G5.Models.ViewModels;

public class HomeViewModel
{
    public List<Category> Categories { get; set; } = new();
    public List<Product> Products { get; set; } = new();
    public int? SelectedCategoryId { get; set; }
    public string? Search { get; set; }
    public string Sort { get; set; } = "";
    public string? SearchError { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
}