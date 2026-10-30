using SWP391_G5.Models.ViewModels;

namespace SWP391_G5.Services
{
    public interface IBakerService
    {
        /// <summary>Lấy danh sách task được gán cho baker (tất cả trạng thái trừ Cancelled/Planned chưa gán).</summary>
        Task<List<BakerProductionTaskViewModel>> GetAssignedTasksAsync(int bakerId);

        /// <summary>Lấy chi tiết 1 task kèm danh sách nguyên liệu cần dùng.</summary>
        Task<BakerTaskDetailViewModel?> GetTaskDetailAsync(int taskId, int bakerId);

        /// <summary>Baker xác nhận bắt đầu sản xuất: Assigned → InProgress.</summary>
        Task<ServiceResult> StartTaskAsync(int taskId, int bakerId);

        /// <summary>Baker hoàn tất task: InProgress → Completed, trừ nguyên liệu, cộng thành phẩm.</summary>
        Task<ServiceResult> CompleteTaskAsync(CompleteTaskViewModel model, int bakerId);
    }
}
