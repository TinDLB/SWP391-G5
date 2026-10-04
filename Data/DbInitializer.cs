using Microsoft.EntityFrameworkCore;
using SWP391_G5.Models.Entities;
using SWP391_G5.Services;

namespace SWP391_G5.Data
{
    public static class DbInitializer
    {
        public static void Initialize(BakeryManagementDbContext context, IPasswordService passwordService)
        {
            // Tự động tạo CSDL nếu chưa có
            context.Database.EnsureCreated();

            // Nếu dùng SQLite, tạo các View cần thiết mà EnsureCreated không sinh tự động
            if (context.Database.IsSqlite())
            {
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        CREATE VIEW IF NOT EXISTS vw_LowStockIngredients AS
                        SELECT IngredientId, Name, Unit, StockQty, MinThreshold,
                               (MinThreshold - StockQty) AS ShortageQty
                        FROM   Ingredients
                        WHERE  IsActive = 1 AND StockQty < MinThreshold;
                    ");

                    context.Database.ExecuteSqlRaw(@"
                        CREATE VIEW IF NOT EXISTS vw_PurchaseOrderProgress AS
                        SELECT po.PoId, po.PoCode, po.Status AS PoStatus, poi.PoItemId,
                               i.Name AS IngredientName, i.Unit, poi.OrderedQty,
                               COALESCE(SUM(gri.ReceivedQty - gri.RejectedQty), 0) AS AcceptedQty,
                               (poi.OrderedQty - COALESCE(SUM(gri.ReceivedQty - gri.RejectedQty), 0)) AS RemainingQty
                        FROM   PurchaseOrders po
                        JOIN   PurchaseOrderItems poi ON poi.PoId = po.PoId
                        JOIN   Ingredients i          ON i.IngredientId = poi.IngredientId
                        LEFT JOIN GoodsReceiptItems gri ON gri.PoItemId = poi.PoItemId
                        GROUP BY po.PoId, po.PoCode, po.Status, poi.PoItemId, i.Name, i.Unit, poi.OrderedQty;
                    ");
                }
                catch
                {
                    // View đã tồn tại hoặc bỏ qua nếu lỗi cú pháp
                }
            }

            // Nếu DB đã có dữ liệu thì không seed lại
            if (context.Users.Any())
            {
                return;
            }

            var passwordHash = passwordService.HashPassword("Bakery@123");

            // 1. Seed Users
            var admin = new User
            {
                Email = "tindlbhe171497@fpt.edu.vn",
                FullName = "Admin TinDLB",
                PasswordHash = passwordHash,
                Role = Role.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var cashier = new User
            {
                Email = "thaitqhe171880@fpt.edu.vn",
                FullName = "Cashier ThaiPQ",
                PasswordHash = passwordHash,
                Role = Role.Cashier,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var baker = new User
            {
                Email = "datpmhe176115@fpt.edu.vn",
                FullName = "Baker DatPM",
                PasswordHash = passwordHash,
                Role = Role.Baker,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var cust1 = new User
            {
                Email = "khach01@example.com",
                FullName = "Nguyễn Văn An",
                Phone = "0900000004",
                PasswordHash = passwordHash,
                Role = Role.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var cust2 = new User
            {
                Email = "khach02@example.com",
                FullName = "Trần Thị Bình",
                Phone = "0900000005",
                PasswordHash = passwordHash,
                Role = Role.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.AddRange(admin, cashier, baker, cust1, cust2);
            context.SaveChanges();

            // 2. Seed Categories
            var catCake = new Category { Name = "Bánh kem", Description = "Bánh kem tươi các loại", IsActive = true };
            var catBread = new Category { Name = "Bánh mì", Description = "Bánh mì ngọt và bánh mì tươi", IsActive = true };
            var catPastry = new Category { Name = "Bánh ngọt", Description = "Bánh ngọt cá nhân, cupcake", IsActive = true };
            var catCookie = new Category { Name = "Bánh quy", Description = "Bánh quy và cookie", IsActive = true };

            context.Categories.AddRange(catCake, catBread, catPastry, catCookie);
            context.SaveChanges();

            // 3. Seed Products
            var prod1 = new Product { CategoryId = catCake.CategoryId, Name = "Bánh kem dâu tây", Description = "Bánh bông lan kem tươi phủ dâu tây, đường kính 20cm", Price = 350000, StockQty = 5, IsActive = true };
            var prod2 = new Product { CategoryId = catBread.CategoryId, Name = "Bánh mì bơ sữa", Description = "Bánh mì mềm thơm bơ sữa, bán theo cái", Price = 15000, StockQty = 40, IsActive = true };
            var prod3 = new Product { CategoryId = catPastry.CategoryId, Name = "Cupcake socola", Description = "Cupcake socola đậm vị cacao", Price = 25000, StockQty = 24, IsActive = true };
            var prod4 = new Product { CategoryId = catCookie.CategoryId, Name = "Bánh quy bơ", Description = "Bánh quy bơ giòn tan, bán theo cái", Price = 6000, StockQty = 60, IsActive = true };

            context.Products.AddRange(prod1, prod2, prod3, prod4);
            context.SaveChanges();

            // 4. Seed Ingredients
            var ing1 = new Ingredient { Name = "Bột mì đa dụng", Unit = "kg", StockQty = 50, MinThreshold = 10, IsActive = true };
            var ing2 = new Ingredient { Name = "Bột mì số 13", Unit = "kg", StockQty = 40, MinThreshold = 10, IsActive = true };
            var ing3 = new Ingredient { Name = "Đường trắng", Unit = "kg", StockQty = 30, MinThreshold = 5, IsActive = true };
            var ing4 = new Ingredient { Name = "Bơ lạt", Unit = "kg", StockQty = 20, MinThreshold = 5, IsActive = true };
            var ing5 = new Ingredient { Name = "Trứng gà", Unit = "quả", StockQty = 180, MinThreshold = 60, IsActive = true };
            var ing6 = new Ingredient { Name = "Sữa tươi không đường", Unit = "lít", StockQty = 30, MinThreshold = 5, IsActive = true };
            var ing7 = new Ingredient { Name = "Kem tươi whipping", Unit = "lít", StockQty = 12, MinThreshold = 15, IsActive = true }; // Tồn thấp
            var ing8 = new Ingredient { Name = "Men khô instant", Unit = "g", StockQty = 1000, MinThreshold = 200, IsActive = true };
            var ing9 = new Ingredient { Name = "Muối", Unit = "g", StockQty = 2000, MinThreshold = 500, IsActive = true };
            var ing10 = new Ingredient { Name = "Bột cacao", Unit = "g", StockQty = 2000, MinThreshold = 500, IsActive = true };
            var ing11 = new Ingredient { Name = "Dâu tây", Unit = "kg", StockQty = 10, MinThreshold = 2, IsActive = true };
            var ing12 = new Ingredient { Name = "Tinh chất vani", Unit = "ml", StockQty = 500, MinThreshold = 100, IsActive = true };

            context.Ingredients.AddRange(ing1, ing2, ing3, ing4, ing5, ing6, ing7, ing8, ing9, ing10, ing11, ing12);
            context.SaveChanges();

            // 5. Seed Recipes
            var rec1 = new Recipe { ProductId = prod1.ProductId, YieldQty = 1, Instructions = "Đánh trứng với đường, trộn bột và bơ, nướng 170°C 35 phút.", IsActive = true };
            var rec2 = new Recipe { ProductId = prod2.ProductId, YieldQty = 10, Instructions = "Ủ bột với men 60 phút, chia 10 phần, ủ lần hai 40 phút, nướng 180°C 18 phút.", IsActive = true };
            var rec3 = new Recipe { ProductId = prod3.ProductId, YieldQty = 12, Instructions = "Trộn khô (bột, cacao), trộn ướt (trứng, bơ, sữa, đường), rót khuôn, nướng 175°C 22 phút.", IsActive = true };
            var rec4 = new Recipe { ProductId = prod4.ProductId, YieldQty = 30, Instructions = "Trộn bơ với đường, thêm trứng và vani, trộn bột, tạo hình, nướng 170°C 14 phút.", IsActive = true };

            context.Recipes.AddRange(rec1, rec2, rec3, rec4);
            context.SaveChanges();

            // 6. Seed Suppliers
            var sup1 = new Supplier { Name = "Công ty TNHH Nguyên liệu bánh Thiên Hương", ContactPerson = "Lê Văn Hương", Phone = "0281000001", Email = "thienhuong@example.com", Address = "Khu công nghiệp A, Hà Nội", IsActive = true };
            var sup2 = new Supplier { Name = "Cơ sở Bơ Sữa Đồng Xanh", ContactPerson = "Phạm Thị Lan", Phone = "0281000002", Email = "dongxanh@example.com", Address = "Mộc Châu, Sơn La", IsActive = true };
            var sup3 = new Supplier { Name = "Công ty TNHH Thực phẩm Vườn Xanh", ContactPerson = "Hoàng Minh Tú", Phone = "0281000003", Email = "vuonxanh@example.com", Address = "Đà Lạt, Lâm Đồng", IsActive = true };

            context.Suppliers.AddRange(sup1, sup2, sup3);
            context.SaveChanges();

            // 7. Seed PurchaseOrders & Items
            var po1 = new PurchaseOrder
            {
                PoCode = "PO-20260914-001",
                SupplierId = sup1.SupplierId,
                CreatedBy = admin.UserId,
                Status = "Completed",
                OrderDate = new DateTime(2026, 9, 14, 9, 0, 0),
                ExpectedDate = new DateOnly(2026, 9, 16),
                TotalAmount = 2650000,
                Note = "Nhập bột, đường, muối, men định kỳ"
            };
            var po2 = new PurchaseOrder
            {
                PoCode = "PO-20260917-002",
                SupplierId = sup2.SupplierId,
                CreatedBy = admin.UserId,
                Status = "PartiallyFulfilled",
                OrderDate = new DateTime(2026, 9, 17, 9, 30, 0),
                ExpectedDate = new DateOnly(2026, 9, 19),
                TotalAmount = 7635000,
                Note = "Nhập bơ, trứng, sữa, kem tươi"
            };
            var po3 = new PurchaseOrder
            {
                PoCode = "PO-20260921-003",
                SupplierId = sup3.SupplierId,
                CreatedBy = admin.UserId,
                Status = "Completed",
                OrderDate = new DateTime(2026, 9, 21, 10, 0, 0),
                ExpectedDate = new DateOnly(2026, 9, 23),
                TotalAmount = 2170000,
                Note = "Nhập dâu tây, cacao, vani"
            };
            var po4 = new PurchaseOrder
            {
                PoCode = "PO-20261001-004",
                SupplierId = sup1.SupplierId,
                CreatedBy = admin.UserId,
                Status = "Sent",
                OrderDate = new DateTime(2026, 10, 1, 8, 30, 0),
                ExpectedDate = new DateOnly(2026, 10, 6),
                TotalAmount = 2060000,
                Note = "Đơn gửi nhà cung cấp bổ sung bột mì và đường cuối tuần"
            };
            var po5 = new PurchaseOrder
            {
                PoCode = "PO-20261004-005",
                SupplierId = sup2.SupplierId,
                CreatedBy = admin.UserId,
                Status = "Draft",
                OrderDate = DateTime.Now,
                ExpectedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
                TotalAmount = 4510000,
                Note = "Dự trù nguyên liệu bơ và sữa làm bánh tuần tới"
            };

            context.PurchaseOrders.AddRange(po1, po2, po3, po4, po5);
            context.SaveChanges();

            var poi1 = new PurchaseOrderItem { PoId = po1.PoId, IngredientId = ing1.IngredientId, OrderedQty = 50, UnitPrice = 22000 };
            var poi2 = new PurchaseOrderItem { PoId = po1.PoId, IngredientId = ing2.IngredientId, OrderedQty = 40, UnitPrice = 24000 };
            var poi3 = new PurchaseOrderItem { PoId = po1.PoId, IngredientId = ing3.IngredientId, OrderedQty = 30, UnitPrice = 21000 };

            var poi4 = new PurchaseOrderItem { PoId = po2.PoId, IngredientId = ing4.IngredientId, OrderedQty = 20, UnitPrice = 210000 };
            var poi5 = new PurchaseOrderItem { PoId = po2.PoId, IngredientId = ing5.IngredientId, OrderedQty = 300, UnitPrice = 3500 };
            var poi6 = new PurchaseOrderItem { PoId = po2.PoId, IngredientId = ing6.IngredientId, OrderedQty = 30, UnitPrice = 32000 };
            var poi7 = new PurchaseOrderItem { PoId = po2.PoId, IngredientId = ing7.IngredientId, OrderedQty = 15, UnitPrice = 95000 };

            var poi8 = new PurchaseOrderItem { PoId = po3.PoId, IngredientId = ing11.IngredientId, OrderedQty = 10, UnitPrice = 120000 };
            var poi9 = new PurchaseOrderItem { PoId = po3.PoId, IngredientId = ing10.IngredientId, OrderedQty = 2000, UnitPrice = 260 };
            var poi10 = new PurchaseOrderItem { PoId = po3.PoId, IngredientId = ing12.IngredientId, OrderedQty = 500, UnitPrice = 900 };

            var poi11 = new PurchaseOrderItem { PoId = po4.PoId, IngredientId = ing1.IngredientId, OrderedQty = 40, UnitPrice = 22000 };
            var poi12 = new PurchaseOrderItem { PoId = po4.PoId, IngredientId = ing2.IngredientId, OrderedQty = 30, UnitPrice = 24000 };
            var poi13 = new PurchaseOrderItem { PoId = po4.PoId, IngredientId = ing3.IngredientId, OrderedQty = 20, UnitPrice = 23000 };

            var poi14 = new PurchaseOrderItem { PoId = po5.PoId, IngredientId = ing4.IngredientId, OrderedQty = 15, UnitPrice = 210000 };
            var poi15 = new PurchaseOrderItem { PoId = po5.PoId, IngredientId = ing6.IngredientId, OrderedQty = 25, UnitPrice = 32000 };
            var poi16 = new PurchaseOrderItem { PoId = po5.PoId, IngredientId = ing7.IngredientId, OrderedQty = 6, UnitPrice = 95000 };

            context.PurchaseOrderItems.AddRange(
                poi1, poi2, poi3, poi4, poi5, poi6, poi7, poi8, poi9, poi10, poi11, poi12, poi13, poi14, poi15, poi16
            );
            context.SaveChanges();

            // Phiếu nhận hàng (GoodsReceipts) để View tiến độ VwPurchaseOrderProgress có dữ liệu
            var gr1 = new GoodsReceipt { PoId = po1.PoId, ReceivedBy = admin.UserId, ReceivedDate = new DateTime(2026, 9, 16, 10, 30, 0), TotalCost = 2650000, PaymentStatus = "Paid", Note = "Đã nhận đủ, đạt chất lượng" };
            var gr2 = new GoodsReceipt { PoId = po2.PoId, ReceivedBy = admin.UserId, ReceivedDate = new DateTime(2026, 9, 19, 14, 0, 0), TotalCost = 6780000, PaymentStatus = "Paid", Note = "Đợt 1: Trứng giao thiếu 120 quả, 3 lít kem tươi bị trả lại" };
            var gr3 = new GoodsReceipt { PoId = po3.PoId, ReceivedBy = admin.UserId, ReceivedDate = new DateTime(2026, 9, 23, 9, 15, 0), TotalCost = 2170000, PaymentStatus = "Paid", Note = "Đã nhận đủ, đạt chất lượng" };
            context.GoodsReceipts.AddRange(gr1, gr2, gr3);
            context.SaveChanges();

            context.GoodsReceiptItems.AddRange(
                new GoodsReceiptItem { ReceiptId = gr1.ReceiptId, PoItemId = poi1.PoItemId, ReceivedQty = 50, RejectedQty = 0 },
                new GoodsReceiptItem { ReceiptId = gr1.ReceiptId, PoItemId = poi2.PoItemId, ReceivedQty = 40, RejectedQty = 0 },
                new GoodsReceiptItem { ReceiptId = gr1.ReceiptId, PoItemId = poi3.PoItemId, ReceivedQty = 30, RejectedQty = 0 },
                new GoodsReceiptItem { ReceiptId = gr2.ReceiptId, PoItemId = poi4.PoItemId, ReceivedQty = 20, RejectedQty = 0 },
                new GoodsReceiptItem { ReceiptId = gr2.ReceiptId, PoItemId = poi5.PoItemId, ReceivedQty = 180, RejectedQty = 0 }, // Đặt 300 nhận 180
                new GoodsReceiptItem { ReceiptId = gr2.ReceiptId, PoItemId = poi6.PoItemId, ReceivedQty = 30, RejectedQty = 0 },
                new GoodsReceiptItem { ReceiptId = gr2.ReceiptId, PoItemId = poi7.PoItemId, ReceivedQty = 15, RejectedQty = 3, RejectReason = "Hạn sử dụng dưới 3 ngày" }, // 3 lít lỗi
                new GoodsReceiptItem { ReceiptId = gr3.ReceiptId, PoItemId = poi8.PoItemId, ReceivedQty = 10, RejectedQty = 0 },
                new GoodsReceiptItem { ReceiptId = gr3.ReceiptId, PoItemId = poi9.PoItemId, ReceivedQty = 2000, RejectedQty = 0 },
                new GoodsReceiptItem { ReceiptId = gr3.ReceiptId, PoItemId = poi10.PoItemId, ReceivedQty = 500, RejectedQty = 0 }
            );
            context.SaveChanges();

            // 8. Seed Địa chỉ cho khách hàng
            var addr1 = new Address
            {
                UserId = cust1.UserId,
                ReceiverName = "Nguyễn Văn An",
                Phone = "0900000004",
                AddressLine = "Số 123 Đường Cầu Giấy, Hà Nội",
                IsDefault = true
            };
            context.Addresses.Add(addr1);
            context.SaveChanges();

            // 9. Seed Vouchers
            var v1 = new Voucher
            {
                Code = "WELCOME10",
                DiscountType = "Percent",
                DiscountValue = 10,
                MinOrderValue = 100000,
                MaxDiscount = 50000,
                StartDate = DateOnly.FromDateTime(DateTime.Today),
                EndDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(3)),
                IsActive = true
            };
            context.Vouchers.Add(v1);
            context.SaveChanges();

            // 10. Seed Đơn hàng bán ra (Orders)
            var ord1 = new Order
            {
                OrderCode = "ORD-20261002-001",
                Channel = "Online",
                CustomerId = cust1.UserId,
                AddressId = addr1.AddressId,
                Status = "Pending",
                PaymentMethod = "COD",
                PaymentStatus = "Unpaid",
                Subtotal = 350000,
                DiscountAmount = 0,
                TotalAmount = 350000,
                Note = "Giao buổi chiều sau 15h",
                CreatedAt = DateTime.Now.AddHours(-10)
            };
            var ord2 = new Order
            {
                OrderCode = "ORD-20261003-002",
                Channel = "Online",
                CustomerId = cust1.UserId,
                AddressId = addr1.AddressId,
                Status = "Preparing",
                PaymentMethod = "Online",
                PaymentStatus = "Paid",
                TransactionRef = "VNPAY-20261003-999",
                Subtotal = 75000,
                DiscountAmount = 0,
                TotalAmount = 75000,
                Note = "Bánh ăn liền đóng hộp cẩn thận",
                CreatedAt = DateTime.Now.AddHours(-5)
            };
            var ord3 = new Order
            {
                OrderCode = "ORD-20261004-003",
                Channel = "Offline",
                CashierId = cashier.UserId,
                Status = "Completed",
                PaymentMethod = "Cash",
                PaymentStatus = "Paid",
                Subtotal = 60000,
                DiscountAmount = 0,
                TotalAmount = 60000,
                Note = "Khách mua tại quầy",
                CreatedAt = DateTime.Now.AddMinutes(-45),
                CompletedAt = DateTime.Now.AddMinutes(-40)
            };

            context.Orders.AddRange(ord1, ord2, ord3);
            context.SaveChanges();

            context.OrderItems.AddRange(
                new OrderItem { OrderId = ord1.OrderId, ProductId = prod1.ProductId, Quantity = 1, UnitPrice = 350000 },
                new OrderItem { OrderId = ord2.OrderId, ProductId = prod3.ProductId, Quantity = 3, UnitPrice = 25000 },
                new OrderItem { OrderId = ord3.OrderId, ProductId = prod4.ProductId, Quantity = 10, UnitPrice = 6000 }
            );
            context.SaveChanges();
        }
    }
}
