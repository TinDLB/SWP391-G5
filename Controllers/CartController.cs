using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Data;
using SWP391_G5.Models.Entities;

namespace SWP391_G5.Controllers
{
    public class CartController : Controller
    {
        private readonly BakeryManagementDbContext _context;
        private readonly ILogger<CartController> _logger;
        public const string CartSessionKey = "BMS_Cart_Session";

        public CartController(BakeryManagementDbContext context, ILogger<CartController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Cart/Index (UC-04: Shopping Cart)
        public async Task<IActionResult> Index()
        {
            try
            {
                var cartItems = await GetCurrentCartAsync();
                return View(cartItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải giỏ hàng");
                return View(new List<CartItem>());
            }
        }

        // POST: /Cart/AddToCart
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            try
            {
                if (quantity <= 0) quantity = 1;

                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == productId && p.IsActive);
                if (product == null)
                {
                    return Json(new { success = false, message = "Sản phẩm không tồn tại hoặc đã ngừng kinh doanh." });
                }

                if (product.StockQty < quantity)
                {
                    return Json(new { success = false, message = $"Sản phẩm chỉ còn {product.StockQty} cái trong kho." });
                }

                int totalCount = 0;

                if (User.Identity?.IsAuthenticated == true)
                {
                    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (int.TryParse(userIdStr, out int userId))
                    {
                        var existingItem = await _context.CartItems
                            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

                        if (existingItem != null)
                        {
                            if (product.StockQty < (existingItem.Quantity + quantity))
                            {
                                return Json(new { success = false, message = $"Bạn đã có {existingItem.Quantity} cái trong giỏ. Tồn kho chỉ còn {product.StockQty} cái." });
                            }
                            existingItem.Quantity += quantity;
                        }
                        else
                        {
                            _context.CartItems.Add(new CartItem
                            {
                                UserId = userId,
                                ProductId = productId,
                                Quantity = quantity
                            });
                        }

                        await _context.SaveChangesAsync();
                        totalCount = await _context.CartItems.Where(c => c.UserId == userId).SumAsync(c => c.Quantity);
                    }
                }
                else
                {
                    var sessionCart = GetSessionCart();
                    int currentQty = sessionCart.ContainsKey(productId) ? sessionCart[productId] : 0;
                    if (product.StockQty < (currentQty + quantity))
                    {
                        return Json(new { success = false, message = $"Bạn đã có {currentQty} cái trong giỏ. Tồn kho chỉ còn {product.StockQty} cái." });
                    }

                    if (sessionCart.ContainsKey(productId))
                    {
                        sessionCart[productId] += quantity;
                    }
                    else
                    {
                        sessionCart[productId] = quantity;
                    }
                    SaveSessionCart(sessionCart);
                    totalCount = sessionCart.Values.Sum();
                }

                return Json(new
                {
                    success = true,
                    message = $"Đã thêm \"{product.Name}\" vào giỏ hàng thành công!",
                    cartCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong AddToCart với productId={ProductId}, quantity={Quantity}", productId, quantity);
                return Json(new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi thêm vào giỏ hàng. Vui lòng thử lại!"
                });
            }
        }

        // GET: /Cart/GetCount
        [HttpGet]
        public async Task<IActionResult> GetCount()
        {
            try
            {
                int totalCount = 0;
                if (User.Identity?.IsAuthenticated == true)
                {
                    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (int.TryParse(userIdStr, out int userId))
                    {
                        totalCount = await _context.CartItems.Where(c => c.UserId == userId).SumAsync(c => c.Quantity);
                    }
                }
                else
                {
                    var sessionCart = GetSessionCart();
                    totalCount = sessionCart.Values.Sum();
                }

                return Json(new { count = totalCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy số lượng giỏ hàng");
                return Json(new { count = 0 });
            }
        }

        // POST: /Cart/UpdateQuantity
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            try
            {
                if (quantity <= 0)
                {
                    return await RemoveItem(productId);
                }

                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    return Json(new { success = false, message = "Sản phẩm không tồn tại." });
                }

                if (product.StockQty < quantity)
                {
                    return Json(new { success = false, message = $"Số lượng vượt quá tồn kho (kho chỉ còn {product.StockQty} cái)." });
                }

                decimal lineTotal = 0;
                int totalCount = 0;
                decimal cartTotal = 0;

                if (User.Identity?.IsAuthenticated == true)
                {
                    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (int.TryParse(userIdStr, out int userId))
                    {
                        var item = await _context.CartItems.Include(c => c.Product).FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);
                        if (item != null)
                        {
                            item.Quantity = quantity;
                            await _context.SaveChangesAsync();
                            lineTotal = item.Quantity * (item.Product?.Price ?? product.Price);
                        }

                        var allItems = await _context.CartItems.Include(c => c.Product).Where(c => c.UserId == userId && c.Product != null).ToListAsync();
                        cartTotal = allItems.Sum(c => c.Quantity * c.Product.Price);
                        totalCount = allItems.Sum(c => c.Quantity);
                    }
                }
                else
                {
                    var sessionCart = GetSessionCart();
                    if (sessionCart.ContainsKey(productId))
                    {
                        sessionCart[productId] = quantity;
                        SaveSessionCart(sessionCart);
                        lineTotal = quantity * product.Price;
                    }

                    var allProducts = await _context.Products.Where(p => sessionCart.Keys.Contains(p.ProductId)).ToListAsync();
                    cartTotal = allProducts.Sum(p => sessionCart[p.ProductId] * p.Price);
                    totalCount = sessionCart.Values.Sum();
                }

                return Json(new
                {
                    success = true,
                    quantity = quantity,
                    lineTotal = lineTotal,
                    formattedLineTotal = lineTotal.ToString("N0") + " đ",
                    cartTotal = cartTotal,
                    formattedCartTotal = cartTotal.ToString("N0") + " đ",
                    cartCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật số lượng productId={ProductId}", productId);
                return Json(new { success = false, message = "Có lỗi xảy ra khi cập nhật số lượng." });
            }
        }

        // POST: /Cart/RemoveItem
        [HttpPost]
        public async Task<IActionResult> RemoveItem(int productId)
        {
            try
            {
                int totalCount = 0;
                decimal cartTotal = 0;

                if (User.Identity?.IsAuthenticated == true)
                {
                    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (int.TryParse(userIdStr, out int userId))
                    {
                        var item = await _context.CartItems.FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);
                        if (item != null)
                        {
                            _context.CartItems.Remove(item);
                            await _context.SaveChangesAsync();
                        }

                        var allItems = await _context.CartItems.Include(c => c.Product).Where(c => c.UserId == userId && c.Product != null).ToListAsync();
                        cartTotal = allItems.Sum(c => c.Quantity * c.Product.Price);
                        totalCount = allItems.Sum(c => c.Quantity);
                    }
                }
                else
                {
                    var sessionCart = GetSessionCart();
                    if (sessionCart.ContainsKey(productId))
                    {
                        sessionCart.Remove(productId);
                        SaveSessionCart(sessionCart);
                    }

                    var allProducts = await _context.Products.Where(p => sessionCart.Keys.Contains(p.ProductId)).ToListAsync();
                    cartTotal = allProducts.Sum(p => sessionCart[p.ProductId] * p.Price);
                    totalCount = sessionCart.Values.Sum();
                }

                return Json(new
                {
                    success = true,
                    message = "Đã xóa sản phẩm khỏi giỏ hàng.",
                    cartTotal = cartTotal,
                    formattedCartTotal = cartTotal.ToString("N0") + " đ",
                    cartCount = totalCount,
                    isEmpty = (totalCount == 0)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa sản phẩm productId={ProductId}", productId);
                return Json(new { success = false, message = "Có lỗi xảy ra khi xóa sản phẩm." });
            }
        }

        // POST: /Cart/ClearCart
        [HttpPost]
        public async Task<IActionResult> ClearCart()
        {
            try
            {
                if (User.Identity?.IsAuthenticated == true)
                {
                    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (int.TryParse(userIdStr, out int userId))
                    {
                        var items = await _context.CartItems.Where(c => c.UserId == userId).ToListAsync();
                        _context.CartItems.RemoveRange(items);
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    HttpContext.Session.Remove(CartSessionKey);
                }

                TempData["InfoMessage"] = "Giỏ hàng đã được làm trống.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi làm trống giỏ hàng");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi làm trống giỏ hàng.";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<CartItem>> GetCurrentCartAsync()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId))
                {
                    return await _context.CartItems
                        .Include(c => c.Product)
                        .ThenInclude(p => p.Category)
                        .Where(c => c.UserId == userId && c.Product != null)
                        .ToListAsync();
                }
            }

            var sessionCart = GetSessionCart();
            var list = new List<CartItem>();
            foreach (var kvp in sessionCart)
            {
                var product = await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.ProductId == kvp.Key);
                if (product != null)
                {
                    list.Add(new CartItem
                    {
                        ProductId = product.ProductId,
                        Product = product,
                        Quantity = kvp.Value
                    });
                }
            }
            return list;
        }

        private Dictionary<int, int> GetSessionCart()
        {
            var json = HttpContext.Session.GetString(CartSessionKey);
            if (string.IsNullOrEmpty(json)) return new Dictionary<int, int>();
            try
            {
                return JsonSerializer.Deserialize<Dictionary<int, int>>(json) ?? new Dictionary<int, int>();
            }
            catch
            {
                return new Dictionary<int, int>();
            }
        }

        private void SaveSessionCart(Dictionary<int, int> cart)
        {
            HttpContext.Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
        }
    }
}
