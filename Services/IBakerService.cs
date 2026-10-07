using SWP391_G5.Models.ViewModels;

namespace SWP391_G5.Services
{
    public interface IBakerService
    {
        Task<List<BakerProductionTaskViewModel>> GetAssignedTasksAsync(int bakerId);
    }
}