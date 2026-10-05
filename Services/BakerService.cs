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

        public async Task<List<BakerProductionTaskViewModel>> GetAssignedTasksAsync(int bakerId)
        {
            return await _context.ProductionTasks
                .Where(t => t.AssignedBaker == bakerId)
                .Include(t => t.Product)
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
    }
}