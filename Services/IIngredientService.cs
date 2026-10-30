using SWP391_G5.Models.ViewModels;

namespace SWP391_G5.Services;

public interface IIngredientService
{
    Task<IngredientListViewModel> GetListAsync(string? keyword, string? filter);
    Task<IngredientDetailViewModel?> GetDetailAsync(int id);
    Task<IngredientFormViewModel?> GetFormAsync(int id);
    Task<ServiceResult> CreateAsync(IngredientFormViewModel model);
    Task<ServiceResult> UpdateAsync(IngredientFormViewModel model);
    Task<ServiceResult> SetActiveAsync(int id, bool isActive);
}