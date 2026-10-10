using SWP391_G5.Models.Entities;

namespace SWP391_G5.Models.ViewModels
{
    public class HomeViewModel
    {
        public List<Category> Categories { get; set; } = new();
        public List<Product> NewCakeCollection { get; set; } = new();
        public List<Product> BirthdayCakes { get; set; } = new();
        public List<Product> BreadAndPastries { get; set; } = new();
    }
}
