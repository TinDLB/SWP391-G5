using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;
using SWP391_G5.Models.ViewModels;

namespace SWP391_G5.Controllers;

public class ProductsController(BakeryManagementDbContext context) : Controller
{
    private Task<Product?> FindProduct(int id) => context.Products.AsNoTracking()
        .Include(p => p.Category)
        .FirstOrDefaultAsync(p => p.ProductId == id && p.IsActive && p.Category.IsActive);

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var product = await FindProduct(id);
        return product == null ? NotFound() : View(product);
    }

    [Authorize(Roles = "Customer"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int id, int quantity)
    {
        var product = await FindProduct(id);
        if (product == null) return NotFound();
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Forbid();
        // Serialize read/modify/write for the same customer's cart.
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var item = await context.CartItems.FindAsync(userId, id);
        var currentQuantity = item?.Quantity ?? 0;
        if (!ModelState.IsValid || quantity < 1 || (long)currentQuantity + quantity > product.StockQty)
        {
            ModelState.AddModelError("", $"Số lượng phải từ 1 và tổng trong giỏ không được vượt quá tồn kho ({product.StockQty} bánh). Trong giỏ hiện có {currentQuantity} bánh.");
            return View("Details", product);
        }
        if (item == null) context.CartItems.Add(new CartItem { UserId = userId, ProductId = id, Quantity = quantity });
        else item.Quantity += quantity;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["SuccessMessage"] = $"Đã thêm {quantity} bánh {product.Name} vào giỏ.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Customer"), HttpGet]
    public async Task<IActionResult> Cart()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Forbid();
        return View(await context.CartItems.AsNoTracking().Include(c => c.Product)
            .ThenInclude(p => p.Category).Where(c => c.UserId == userId).ToListAsync());
    }

    [Authorize(Roles = "Customer"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromCart(int id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Forbid();
        var item = await context.CartItems.FindAsync(userId, id);
        if (item != null) { context.CartItems.Remove(item); await context.SaveChangesAsync(); }
        return RedirectToAction(nameof(Cart));
    }

}
