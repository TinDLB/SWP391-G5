
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.ViewModels;

namespace SWP391_G5.Services
{
    public class BakerService : IBakerService
    {
        private readonly BakeryManagementDbContext _context;

        public BakerService(BakeryManagementDbContext context)
        {
            _context = context;
        }

        public async Task<List<BakerProductionTaskViewModel>>
            GetAssignedTasksAsync(int bakerId)
        {
            return await _context.ProductionTasks
                .AsNoTracking()
                .Where(t => t.AssignedBaker == bakerId)
                .Select(t => new BakerProductionTaskViewModel
                {
                    TaskId = t.TaskId,
                    ProductName = t.Product.Name,
                    TargetQty = t.TargetQty,
                    Status = t.Status,
                    Deadline = t.Deadline
                })
                .OrderBy(t => t.Deadline)
                .ToListAsync();
        }

        public async Task<bool> StartProductionAsync(
            int taskId,
            int bakerId)
        {
            var task = await _context.ProductionTasks
                .FirstOrDefaultAsync(t =>
                    t.TaskId == taskId &&
                    t.AssignedBaker == bakerId);

            if (task == null || task.Status != "Assigned")
            {
                return false;
            }

            task.Status = "InProgress";
            task.StartedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return true;
        }
    }
}