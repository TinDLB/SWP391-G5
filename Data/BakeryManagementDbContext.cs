using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SWP391_G5.Models.Entities;

namespace SWP391_G5.Data;

public partial class BakeryManagementDbContext : DbContext
{
    public BakeryManagementDbContext(DbContextOptions<BakeryManagementDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Address> Addresses { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<GoodsReceipt> GoodsReceipts { get; set; }

    public virtual DbSet<GoodsReceiptItem> GoodsReceiptItems { get; set; }

    public virtual DbSet<Ingredient> Ingredients { get; set; }

    public virtual DbSet<InventoryTransaction> InventoryTransactions { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductionTask> ProductionTasks { get; set; }

    public virtual DbSet<ProductionTaskIngredient> ProductionTaskIngredients { get; set; }

    public virtual DbSet<PurchaseOrder> PurchaseOrders { get; set; }

    public virtual DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }

    public virtual DbSet<Recipe> Recipes { get; set; }

    public virtual DbSet<RecipeItem> RecipeItems { get; set; }

    public virtual DbSet<StockAdjustment> StockAdjustments { get; set; }

    public virtual DbSet<StockCount> StockCounts { get; set; }

    public virtual DbSet<StockCountItem> StockCountItems { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    public virtual DbSet<VwLowStockIngredient> VwLowStockIngredients { get; set; }

    public virtual DbSet<VwPurchaseOrderProgress> VwPurchaseOrderProgresses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isSqlServer = Database.IsSqlServer();
        if (isSqlServer)
        {
            modelBuilder.UseCollation("Vietnamese_CI_AS");
        }
        var nowSql = isSqlServer ? "(sysdatetime())" : "CURRENT_TIMESTAMP";

        modelBuilder.Entity<Address>(entity =>
        {
            entity.HasKey(e => e.AddressId).HasName("PK__Addresse__091C2AFBD192F110");

            entity.Property(e => e.AddressLine).HasMaxLength(255);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ReceiverName).HasMaxLength(100);

            entity.HasOne(d => d.User).WithMany(p => p.Addresses)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Addresses__UserI__4F7CD00D");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.ProductId });

            entity.HasOne(d => d.Product).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CartItems__Produ__3493CFA7");

            entity.HasOne(d => d.User).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CartItems__UserI__339FAB6E");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Categori__19093A0BD03E8E57");

            entity.HasIndex(e => e.Name, "UQ_Categories_Name").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<GoodsReceipt>(entity =>
        {
            entity.HasKey(e => e.ReceiptId).HasName("PK__GoodsRec__CC08C420879F933B");

            entity.HasIndex(e => e.PoId, "IX_GR_Po");

            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("Unpaid");
            entity.Property(e => e.ReceivedDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.TotalCost).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Po).WithMany(p => p.GoodsReceipts)
                .HasForeignKey(d => d.PoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__GoodsRecei__PoId__03F0984C");

            entity.HasOne(d => d.ReceivedByNavigation).WithMany(p => p.GoodsReceipts)
                .HasForeignKey(d => d.ReceivedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__GoodsRece__Recei__04E4BC85");
        });

        modelBuilder.Entity<GoodsReceiptItem>(entity =>
        {
            entity.HasKey(e => e.ReceiptItemId).HasName("PK__GoodsRec__AF7BE10D6F7CF73A");

            entity.HasIndex(e => new { e.ReceiptId, e.PoItemId }, "UQ_GoodsReceiptItems").IsUnique();

            entity.Property(e => e.ReceivedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.RejectReason).HasMaxLength(255);
            entity.Property(e => e.RejectedQty).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.PoItem).WithMany(p => p.GoodsReceiptItems)
                .HasForeignKey(d => d.PoItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__GoodsRece__PoIte__0D7A0286");

            entity.HasOne(d => d.Receipt).WithMany(p => p.GoodsReceiptItems)
                .HasForeignKey(d => d.ReceiptId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__GoodsRece__Recei__0C85DE4D");
        });

        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(e => e.IngredientId).HasName("PK__Ingredie__BEAEB25AAAA9A2E2");

            entity.HasIndex(e => e.Name, "UQ_Ingredients_Name").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MinThreshold).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.StockQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Unit).HasMaxLength(20);
        });

        modelBuilder.Entity<InventoryTransaction>(entity =>
        {
            entity.HasKey(e => e.TxnId).HasName("PK__Inventor__C19608542F2E248F");

            entity.HasIndex(e => new { e.IngredientId, e.CreatedAt }, "IX_InvTxn_Ingredient");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql(nowSql);
            entity.Property(e => e.Note).HasMaxLength(255);
            entity.Property(e => e.QtyChange).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.RefTable)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.TxnType)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InventoryTransactions)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__Inventory__Creat__14270015");

            entity.HasOne(d => d.Ingredient).WithMany(p => p.InventoryTransactions)
                .HasForeignKey(d => d.IngredientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventory__Ingre__1332DBDC");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Invoices__D796AAB5FD355116");

            entity.HasIndex(e => e.InvoiceNo, "UQ_Invoices_No").IsUnique();

            entity.HasIndex(e => e.OrderId, "UQ_Invoices_Order").IsUnique();

            entity.Property(e => e.InvoiceNo)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.IssuedAt).HasDefaultValueSql(nowSql);

            entity.HasOne(d => d.IssuedByNavigation).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.IssuedBy)
                .HasConstraintName("FK__Invoices__Issued__503BEA1C");

            entity.HasOne(d => d.Order).WithOne(p => p.Invoice)
                .HasForeignKey<Invoice>(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Invoices__OrderI__4F47C5E3");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E12C6FFCC98");

            entity.HasIndex(e => new { e.UserId, e.IsRead }, "IX_Notif_User");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql(nowSql);
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.NotificationType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(150);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Notificat__UserI__540C7B00");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Orders__C3905BCF973AD6EB");

            entity.HasIndex(e => e.CustomerId, "IX_Orders_Customer");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_Orders_Status_Date");

            entity.HasIndex(e => e.OrderCode, "UQ_Orders_Code").IsUnique();

            entity.Property(e => e.Channel)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql(nowSql);
            entity.Property(e => e.DeliveryStaffName).HasMaxLength(100);
            entity.Property(e => e.DeliveryStaffPhone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.OrderCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("Unpaid");
            entity.Property(e => e.Status)
                .HasMaxLength(12)
                .IsUnicode(false)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TransactionRef)
                .HasMaxLength(60)
                .IsUnicode(false);

            entity.HasOne(d => d.Address).WithMany(p => p.Orders)
                .HasForeignKey(d => d.AddressId)
                .HasConstraintName("FK__Orders__AddressI__3B40CD36");

            entity.HasOne(d => d.Cashier).WithMany(p => p.OrderCashiers)
                .HasForeignKey(d => d.CashierId)
                .HasConstraintName("FK__Orders__CashierI__3A4CA8FD");

            entity.HasOne(d => d.Customer).WithMany(p => p.OrderCustomers)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__Orders__Customer__395884C4");

            entity.HasOne(d => d.Voucher).WithMany(p => p.Orders)
                .HasForeignKey(d => d.VoucherId)
                .HasConstraintName("FK__Orders__VoucherI__3C34F16F");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.OrderItemId).HasName("PK__OrderIte__57ED068175AE27BC");

            entity.HasIndex(e => new { e.OrderId, e.ProductId }, "UQ_OrderItems").IsUnique();

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderItem__Order__489AC854");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderItem__Produ__498EEC8D");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Products__B40CC6CD0C5C131A");

            entity.HasIndex(e => e.Name, "UQ_Products_Name").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ImageUrl).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Products__Catego__5812160E");
        });

        modelBuilder.Entity<ProductionTask>(entity =>
        {
            entity.HasKey(e => e.TaskId).HasName("PK__Producti__7C6949B1D9AB60D3");

            entity.HasIndex(e => new { e.AssignedBaker, e.Status }, "IX_ProdTasks_Baker");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql(nowSql);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(12)
                .IsUnicode(false)
                .HasDefaultValue("Planned");

            entity.HasOne(d => d.AssignedBakerNavigation).WithMany(p => p.ProductionTaskAssignedBakerNavigations)
                .HasForeignKey(d => d.AssignedBaker)
                .HasConstraintName("FK__Productio__Assig__5CA1C101");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ProductionTaskCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Productio__Creat__5BAD9CC8");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductionTasks)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Productio__Produ__59C55456");
        });

        modelBuilder.Entity<ProductionTaskIngredient>(entity =>
        {
            entity.HasKey(e => new { e.TaskId, e.IngredientId }).HasName("PK_ProdTaskIng");

            entity.Property(e => e.RequiredQty).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.Ingredient).WithMany(p => p.ProductionTaskIngredients)
                .HasForeignKey(d => d.IngredientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Productio__Ingre__65370702");

            entity.HasOne(d => d.Task).WithMany(p => p.ProductionTaskIngredients)
                .HasForeignKey(d => d.TaskId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Productio__TaskI__6442E2C9");
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.PoId).HasName("PK__Purchase__A4C01E7E6B6AF2AF");

            entity.HasIndex(e => new { e.SupplierId, e.Status }, "IX_PO_Supplier_Status");

            entity.HasIndex(e => e.PoCode, "UQ_PurchaseOrders_Code").IsUnique();

            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.OrderDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.PoCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Draft");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseO__Creat__76969D2E");

            entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseOrders)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseO__Suppl__75A278F5");
        });

        modelBuilder.Entity<PurchaseOrderItem>(entity =>
        {
            entity.HasKey(e => e.PoItemId).HasName("PK__Purchase__F123206737638783");

            entity.HasIndex(e => new { e.PoId, e.IngredientId }, "UQ_PoItems").IsUnique();

            entity.Property(e => e.OrderedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Ingredient).WithMany(p => p.PurchaseOrderItems)
                .HasForeignKey(d => d.IngredientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseO__Ingre__7F2BE32F");

            entity.HasOne(d => d.Po).WithMany(p => p.PurchaseOrderItems)
                .HasForeignKey(d => d.PoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PurchaseOr__PoId__7E37BEF6");
        });

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.RecipeId).HasName("PK__Recipes__FDD988B0F902A518");

            entity.HasIndex(e => e.ProductId, "UQ_Recipes_Product").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Product).WithOne(p => p.Recipe)
                .HasForeignKey<Recipe>(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Recipes__Product__6754599E");
        });

        modelBuilder.Entity<RecipeItem>(entity =>
        {
            entity.HasKey(e => new { e.RecipeId, e.IngredientId });

            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");

            entity.HasOne(d => d.Ingredient).WithMany(p => p.RecipeItems)
                .HasForeignKey(d => d.IngredientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RecipeIte__Ingre__6D0D32F4");

            entity.HasOne(d => d.Recipe).WithMany(p => p.RecipeItems)
                .HasForeignKey(d => d.RecipeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RecipeIte__Recip__6C190EBB");
        });

        modelBuilder.Entity<StockAdjustment>(entity =>
        {
            entity.HasKey(e => e.AdjustmentId).HasName("PK__StockAdj__E60DB8937BDD470B");

            entity.Property(e => e.AdjustType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql(nowSql);
            entity.Property(e => e.Note).HasMaxLength(255);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Reason)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StockAdjustments)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StockAdju__Creat__1AD3FDA4");

            entity.HasOne(d => d.Ingredient).WithMany(p => p.StockAdjustments)
                .HasForeignKey(d => d.IngredientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StockAdju__Ingre__19DFD96B");
        });

        modelBuilder.Entity<StockCount>(entity =>
        {
            entity.HasKey(e => e.CountId).HasName("PK__StockCou__06678B7CB64E3DB1");

            entity.Property(e => e.CountDate).HasDefaultValueSql(nowSql);
            entity.Property(e => e.Note).HasMaxLength(255);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StockCounts)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StockCoun__Creat__22751F6C");
        });

        modelBuilder.Entity<StockCountItem>(entity =>
        {
            entity.HasKey(e => e.CountItemId).HasName("PK__StockCou__585EF925F25C57F0");

            entity.HasIndex(e => new { e.CountId, e.IngredientId }, "UQ_StockCountItems").IsUnique();

            entity.Property(e => e.CountedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.SystemQty).HasColumnType("decimal(18, 3)");
            if (isSqlServer)
            {
                entity.Property(e => e.Variance)
                    .HasComputedColumnSql("([CountedQty]-[SystemQty])", true)
                    .HasColumnType("decimal(19, 3)");
            }
            else
            {
                entity.Property(e => e.Variance)
                    .HasComputedColumnSql("(CountedQty - SystemQty)");
            }

            entity.HasOne(d => d.Count).WithMany(p => p.StockCountItems)
                .HasForeignKey(d => d.CountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StockCoun__Count__2645B050");

            entity.HasOne(d => d.Ingredient).WithMany(p => p.StockCountItems)
                .HasForeignKey(d => d.IngredientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StockCoun__Ingre__2739D489");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("PK__Supplier__4BE666B4D6D6C7C7");

            entity.HasIndex(e => e.Name, "UQ_Suppliers_Name").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.ContactPerson).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4CD7F2D9CC");

            entity.HasIndex(e => e.Email, "UQ_Users_Email").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql(nowSql);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.VoucherId).HasName("PK__Vouchers__3AEE79216476DC98");

            entity.HasIndex(e => e.Code, "UQ_Vouchers_Code").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.DiscountType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MaxDiscount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinOrderValue).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<VwLowStockIngredient>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_LowStockIngredients");

            entity.Property(e => e.IngredientId).ValueGeneratedOnAdd();
            entity.Property(e => e.MinThreshold).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.ShortageQty).HasColumnType("decimal(19, 3)");
            entity.Property(e => e.StockQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Unit).HasMaxLength(20);
        });

        modelBuilder.Entity<VwPurchaseOrderProgress>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_PurchaseOrderProgress");

            entity.Property(e => e.AcceptedQty).HasColumnType("decimal(38, 3)");
            entity.Property(e => e.IngredientName).HasMaxLength(100);
            entity.Property(e => e.OrderedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.PoCode)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.PoStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.RemainingQty).HasColumnType("decimal(38, 3)");
            entity.Property(e => e.Unit).HasMaxLength(20);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
