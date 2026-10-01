
SET NOCOUNT ON;
GO

IF DB_ID(N'BakeryManagementDB') IS NULL
    CREATE DATABASE BakeryManagementDB COLLATE Vietnamese_CI_AS;
GO
USE BakeryManagementDB;
GO


DROP VIEW IF EXISTS vw_PurchaseOrderProgress;
DROP VIEW IF EXISTS vw_LowStockIngredients;
GO
DROP TABLE IF EXISTS ProductionTaskIngredients;
DROP TABLE IF EXISTS ProductionTasks;
DROP TABLE IF EXISTS Notifications;
DROP TABLE IF EXISTS Invoices;
DROP TABLE IF EXISTS OrderItems;
DROP TABLE IF EXISTS Orders;
DROP TABLE IF EXISTS CartItems;
DROP TABLE IF EXISTS Vouchers;
DROP TABLE IF EXISTS StockCountItems;
DROP TABLE IF EXISTS StockCounts;
DROP TABLE IF EXISTS StockAdjustments;
DROP TABLE IF EXISTS InventoryTransactions;
DROP TABLE IF EXISTS GoodsReceiptItems;
DROP TABLE IF EXISTS GoodsReceipts;
DROP TABLE IF EXISTS PurchaseOrderItems;
DROP TABLE IF EXISTS PurchaseOrders;
DROP TABLE IF EXISTS Suppliers;
DROP TABLE IF EXISTS RecipeItems;
DROP TABLE IF EXISTS Recipes;
DROP TABLE IF EXISTS Ingredients;
DROP TABLE IF EXISTS Products;
DROP TABLE IF EXISTS Categories;
DROP TABLE IF EXISTS Addresses;
DROP TABLE IF EXISTS Users;
GO

CREATE TABLE Users (
    UserId       INT IDENTITY(1,1) PRIMARY KEY,
    Email        NVARCHAR(150) NOT NULL,
    PasswordHash VARCHAR(100)  NOT NULL,           -- BCrypt (60 ký tự)
    FullName     NVARCHAR(100) NOT NULL,
    Phone        VARCHAR(20)   NULL,
    Role         VARCHAR(20)   NOT NULL,
    IsActive     BIT           NOT NULL DEFAULT 1,
    CreatedAt    DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT CK_Users_Role  CHECK (Role IN ('Customer','Cashier','Baker','Admin'))
);

CREATE TABLE Addresses (
    AddressId    INT IDENTITY(1,1) PRIMARY KEY,
    UserId       INT           NOT NULL REFERENCES Users(UserId),
    ReceiverName NVARCHAR(100) NOT NULL,
    Phone        VARCHAR(20)   NOT NULL,
    AddressLine  NVARCHAR(255) NOT NULL,
    IsDefault    BIT           NOT NULL DEFAULT 0
);

/* ---- 2.2 Sản phẩm, công thức, nguyên liệu (UC-21, UC-22, UC-25) ---- */
CREATE TABLE Categories (
    CategoryId   INT IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(100) NOT NULL,
    Description  NVARCHAR(255) NULL,
    IsActive     BIT           NOT NULL DEFAULT 1,
    CONSTRAINT UQ_Categories_Name UNIQUE (Name)
);

CREATE TABLE Products (
    ProductId    INT IDENTITY(1,1) PRIMARY KEY,
    CategoryId   INT            NOT NULL REFERENCES Categories(CategoryId),
    Name         NVARCHAR(150)  NOT NULL,
    Description  NVARCHAR(500)  NULL,
    ImageUrl     NVARCHAR(255)  NULL,
    Price        DECIMAL(18,2)  NOT NULL,
    StockQty     INT            NOT NULL DEFAULT 0,   -- tồn kho THÀNH PHẨM
    IsActive     BIT            NOT NULL DEFAULT 1,
    CONSTRAINT UQ_Products_Name  UNIQUE (Name),
    CONSTRAINT CK_Products_Price CHECK (Price >= 0),
    CONSTRAINT CK_Products_Stock CHECK (StockQty >= 0)
);

CREATE TABLE Ingredients (
    IngredientId INT IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(100)  NOT NULL,
    Unit         NVARCHAR(20)   NOT NULL,             -- kg, g, lít, ml, quả
    StockQty     DECIMAL(18,3)  NOT NULL DEFAULT 0,   -- luôn = SUM(InventoryTransactions)
    MinThreshold DECIMAL(18,3)  NOT NULL DEFAULT 0,   -- dưới ngưỡng -> chỉ cảnh báo
    IsActive     BIT            NOT NULL DEFAULT 1,
    CONSTRAINT UQ_Ingredients_Name UNIQUE (Name),
    CONSTRAINT CK_Ingredients_Stock CHECK (StockQty >= 0),
    CONSTRAINT CK_Ingredients_Min   CHECK (MinThreshold >= 0)
);

CREATE TABLE Recipes (
    RecipeId     INT IDENTITY(1,1) PRIMARY KEY,
    ProductId    INT            NOT NULL REFERENCES Products(ProductId),
    YieldQty     INT            NOT NULL,             -- 1 mẻ ra bao nhiêu sản phẩm
    Instructions NVARCHAR(MAX)  NULL,
    IsActive     BIT            NOT NULL DEFAULT 1,
    CONSTRAINT UQ_Recipes_Product UNIQUE (ProductId), -- 1 sản phẩm - 1 công thức
    CONSTRAINT CK_Recipes_Yield   CHECK (YieldQty > 0)
);

CREATE TABLE RecipeItems (                            -- định lượng cho 1 mẻ
    RecipeId     INT            NOT NULL REFERENCES Recipes(RecipeId),
    IngredientId INT            NOT NULL REFERENCES Ingredients(IngredientId),
    Quantity     DECIMAL(18,3)  NOT NULL,
    CONSTRAINT PK_RecipeItems PRIMARY KEY (RecipeId, IngredientId),
    CONSTRAINT CK_RecipeItems_Qty CHECK (Quantity > 0)
);

/* ---- 2.3 Nhà cung cấp & đơn đặt hàng (UC-17, UC-31, UC-32) --------- */
CREATE TABLE Suppliers (
    SupplierId    INT IDENTITY(1,1) PRIMARY KEY,
    Name          NVARCHAR(150) NOT NULL,
    ContactPerson NVARCHAR(100) NULL,
    Phone         VARCHAR(20)   NULL,
    Email         NVARCHAR(150) NULL,
    Address       NVARCHAR(255) NULL,
    IsActive      BIT           NOT NULL DEFAULT 1,
    CONSTRAINT UQ_Suppliers_Name UNIQUE (Name)
);

CREATE TABLE PurchaseOrders (
    PoId         INT IDENTITY(1,1) PRIMARY KEY,
    PoCode       VARCHAR(30)   NOT NULL,              -- giữ nguyên qua mọi đợt giao
    SupplierId   INT           NOT NULL REFERENCES Suppliers(SupplierId),
    CreatedBy    INT           NOT NULL REFERENCES Users(UserId),
    Status       VARCHAR(20)   NOT NULL DEFAULT 'Draft',
    OrderDate    DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
    ExpectedDate DATE          NULL,
    TotalAmount  DECIMAL(18,2) NOT NULL DEFAULT 0,    -- giá trị ĐẶT (không phải giá trị nhận)
    Note         NVARCHAR(500) NULL,
    CONSTRAINT UQ_PurchaseOrders_Code UNIQUE (PoCode),
    CONSTRAINT CK_PurchaseOrders_Status
        CHECK (Status IN ('Draft','Sent','PartiallyFulfilled','Completed','Cancelled'))
);

CREATE TABLE PurchaseOrderItems (
    PoItemId     INT IDENTITY(1,1) PRIMARY KEY,
    PoId         INT            NOT NULL REFERENCES PurchaseOrders(PoId),
    IngredientId INT            NOT NULL REFERENCES Ingredients(IngredientId),
    OrderedQty   DECIMAL(18,3)  NOT NULL,
    UnitPrice    DECIMAL(18,2)  NOT NULL,
    CONSTRAINT UQ_PoItems UNIQUE (PoId, IngredientId),
    CONSTRAINT CK_PoItems_Qty   CHECK (OrderedQty > 0),
    CONSTRAINT CK_PoItems_Price CHECK (UnitPrice >= 0)
);

/* ---- 2.4 Nhập kho (UC-18, UC-32): 1 PO có N phiếu nhận hàng --------- */
CREATE TABLE GoodsReceipts (
    ReceiptId     INT IDENTITY(1,1) PRIMARY KEY,
    PoId          INT           NOT NULL REFERENCES PurchaseOrders(PoId),
    ReceivedBy    INT           NOT NULL REFERENCES Users(UserId),
    ReceivedDate  DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
    TotalCost     DECIMAL(18,2) NOT NULL DEFAULT 0,   -- chỉ tính hàng ĐẠT chất lượng
    PaymentStatus VARCHAR(10)   NOT NULL DEFAULT 'Unpaid',
    PaidAt        DATETIME2     NULL,
    Note          NVARCHAR(500) NULL,
    CONSTRAINT CK_GoodsReceipts_Pay CHECK (PaymentStatus IN ('Unpaid','Paid'))
);

CREATE TABLE GoodsReceiptItems (
    ReceiptItemId INT IDENTITY(1,1) PRIMARY KEY,
    ReceiptId     INT            NOT NULL REFERENCES GoodsReceipts(ReceiptId),
    PoItemId      INT            NOT NULL REFERENCES PurchaseOrderItems(PoItemId),
    ReceivedQty   DECIMAL(18,3)  NOT NULL,            -- số lượng giao đến
    RejectedQty   DECIMAL(18,3)  NOT NULL DEFAULT 0,  -- số lượng bị từ chối (lỗi/hết hạn)
    RejectReason  NVARCHAR(255)  NULL,
    ExpiryDate    DATE           NULL,
    CONSTRAINT UQ_GoodsReceiptItems UNIQUE (ReceiptId, PoItemId),
    CONSTRAINT CK_GRI_Qty CHECK (ReceivedQty >= 0 AND RejectedQty >= 0 AND RejectedQty <= ReceivedQty),
    CONSTRAINT CK_GRI_Reason CHECK (RejectedQty = 0 OR RejectReason IS NOT NULL)
);

/* ---- 2.5 Sổ kho nguyên liệu, điều chỉnh, kiểm kê (UC-19, 33, 34) --- */
CREATE TABLE InventoryTransactions (
    TxnId        INT IDENTITY(1,1) PRIMARY KEY,
    IngredientId INT            NOT NULL REFERENCES Ingredients(IngredientId),
    TxnType      VARCHAR(20)    NOT NULL,
    QtyChange    DECIMAL(18,3)  NOT NULL,             -- có dấu: + nhập, - xuất
    RefTable     VARCHAR(30)    NULL,
    RefId        INT            NULL,
    Note         NVARCHAR(255)  NULL,
    CreatedBy    INT            NULL REFERENCES Users(UserId),
    CreatedAt    DATETIME2      NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_InvTxn_Type CHECK (TxnType IN
        ('Receipt','ProductionUse','AdjustmentIn','AdjustmentOut','StockCount')),
    CONSTRAINT CK_InvTxn_Qty  CHECK (QtyChange <> 0)
);

CREATE TABLE StockAdjustments (
    AdjustmentId INT IDENTITY(1,1) PRIMARY KEY,
    IngredientId INT            NOT NULL REFERENCES Ingredients(IngredientId),
    AdjustType   VARCHAR(10)    NOT NULL,
    Quantity     DECIMAL(18,3)  NOT NULL,
    Reason       VARCHAR(20)    NOT NULL,
    Note         NVARCHAR(255)  NULL,
    CreatedBy    INT            NOT NULL REFERENCES Users(UserId),
    CreatedAt    DATETIME2      NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_StockAdj_Type   CHECK (AdjustType IN ('Increase','Decrease')),
    CONSTRAINT CK_StockAdj_Qty    CHECK (Quantity > 0),
    CONSTRAINT CK_StockAdj_Reason CHECK (Reason IN
        ('Correction','FoundStock','Damaged','Expired','Wastage','Other'))
);

CREATE TABLE StockCounts (
    CountId      INT IDENTITY(1,1) PRIMARY KEY,
    CountDate    DATETIME2      NOT NULL DEFAULT SYSDATETIME(),
    CreatedBy    INT            NOT NULL REFERENCES Users(UserId),
    Note         NVARCHAR(255)  NULL
);

CREATE TABLE StockCountItems (
    CountItemId  INT IDENTITY(1,1) PRIMARY KEY,
    CountId      INT            NOT NULL REFERENCES StockCounts(CountId),
    IngredientId INT            NOT NULL REFERENCES Ingredients(IngredientId),
    SystemQty    DECIMAL(18,3)  NOT NULL,
    CountedQty   DECIMAL(18,3)  NOT NULL,
    Variance AS (CountedQty - SystemQty) PERSISTED,
    CONSTRAINT UQ_StockCountItems UNIQUE (CountId, IngredientId),
    CONSTRAINT CK_StockCountItems_Qty CHECK (SystemQty >= 0 AND CountedQty >= 0)
);

/* ---- 2.6 Voucher & giỏ hàng (UC-13, UC-04) ------------------------- */
CREATE TABLE Vouchers (
    VoucherId     INT IDENTITY(1,1) PRIMARY KEY,
    Code          VARCHAR(30)    NOT NULL,
    DiscountType  VARCHAR(10)    NOT NULL,
    DiscountValue DECIMAL(18,2)  NOT NULL,
    MinOrderValue DECIMAL(18,2)  NOT NULL DEFAULT 0,
    MaxDiscount   DECIMAL(18,2)  NULL,
    StartDate     DATE           NOT NULL,
    EndDate       DATE           NOT NULL,
    UsageLimit    INT            NULL,
    UsedCount     INT            NOT NULL DEFAULT 0,
    IsActive      BIT            NOT NULL DEFAULT 1,
    CONSTRAINT UQ_Vouchers_Code UNIQUE (Code),
    CONSTRAINT CK_Vouchers_Type CHECK (DiscountType IN ('Percent','Amount')),
    CONSTRAINT CK_Vouchers_Val  CHECK (DiscountValue > 0),
    CONSTRAINT CK_Vouchers_Date CHECK (EndDate >= StartDate)
);

CREATE TABLE CartItems (
    UserId       INT NOT NULL REFERENCES Users(UserId),
    ProductId    INT NOT NULL REFERENCES Products(ProductId),
    Quantity     INT NOT NULL,
    CONSTRAINT PK_CartItems PRIMARY KEY (UserId, ProductId),
    CONSTRAINT CK_CartItems_Qty CHECK (Quantity > 0)
);

/* ---- 2.7 Đơn hàng: Online (web) và Offline (POS) - UC-06/07/11/12/14-16 */
CREATE TABLE Orders (
    OrderId       INT IDENTITY(1,1) PRIMARY KEY,
    OrderCode     VARCHAR(30)    NOT NULL,
    Channel       VARCHAR(10)    NOT NULL,            -- Online | Offline
    CustomerId    INT            NULL REFERENCES Users(UserId),   -- NULL = khách vãng lai tại quầy
    CashierId     INT            NULL REFERENCES Users(UserId),   -- nhân viên xử lý
    AddressId     INT            NULL REFERENCES Addresses(AddressId),
    VoucherId     INT            NULL REFERENCES Vouchers(VoucherId),
    Status        VARCHAR(12)    NOT NULL DEFAULT 'Pending',
    PaymentMethod VARCHAR(10)    NOT NULL,
    PaymentStatus VARCHAR(10)    NOT NULL DEFAULT 'Unpaid',
    TransactionRef VARCHAR(60)   NULL,                -- mã giao dịch cổng thanh toán
    Subtotal      DECIMAL(18,2)  NOT NULL DEFAULT 0,
    DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalAmount   DECIMAL(18,2)  NOT NULL DEFAULT 0,
    DeliveryStaffName  NVARCHAR(100) NULL,            -- không có actor Shipper riêng
    DeliveryStaffPhone VARCHAR(20)   NULL,
    Note          NVARCHAR(500)  NULL,
    CreatedAt     DATETIME2      NOT NULL DEFAULT SYSDATETIME(),
    CompletedAt   DATETIME2      NULL,
    CONSTRAINT UQ_Orders_Code UNIQUE (OrderCode),
    CONSTRAINT CK_Orders_Status CHECK (Status IN
        ('Pending','Preparing','Delivering','Completed','Cancelled')),
    CONSTRAINT CK_Orders_PayStatus CHECK (PaymentStatus IN
        ('Unpaid','Paid','Failed','Refunded')),
    -- Hai kênh tách biệt: Online chỉ COD/Online + bắt buộc có khách + có địa chỉ;
    -- Offline chỉ Cash/QR, không có địa chỉ giao.
    CONSTRAINT CK_Orders_Channel_Rules CHECK (
        (Channel = 'Online'  AND PaymentMethod IN ('Online','COD')
                             AND CustomerId IS NOT NULL AND AddressId IS NOT NULL)
     OR (Channel = 'Offline' AND PaymentMethod IN ('Cash','QR')
                             AND AddressId IS NULL)
    )
);

CREATE TABLE OrderItems (
    OrderItemId  INT IDENTITY(1,1) PRIMARY KEY,
    OrderId      INT            NOT NULL REFERENCES Orders(OrderId),
    ProductId    INT            NOT NULL REFERENCES Products(ProductId),
    Quantity     INT            NOT NULL,
    UnitPrice    DECIMAL(18,2)  NOT NULL,             -- giá tại thời điểm bán
    CONSTRAINT UQ_OrderItems UNIQUE (OrderId, ProductId),
    CONSTRAINT CK_OrderItems_Qty CHECK (Quantity > 0)
);

CREATE TABLE Invoices (
    InvoiceId    INT IDENTITY(1,1) PRIMARY KEY,
    OrderId      INT            NOT NULL REFERENCES Orders(OrderId),
    InvoiceNo    VARCHAR(30)    NOT NULL,
    IssuedBy     INT            NULL REFERENCES Users(UserId),
    IssuedAt     DATETIME2      NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_Invoices_Order UNIQUE (OrderId),
    CONSTRAINT UQ_Invoices_No    UNIQUE (InvoiceNo)
);

CREATE TABLE Notifications (
    NotificationId   INT IDENTITY(1,1) PRIMARY KEY,
    UserId           INT            NOT NULL REFERENCES Users(UserId),
    NotificationType VARCHAR(20)    NOT NULL,
    Title            NVARCHAR(150)  NOT NULL,
    Message          NVARCHAR(500)  NULL,
    IsRead           BIT            NOT NULL DEFAULT 0,
    CreatedAt        DATETIME2      NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_Notifications_Type CHECK (NotificationType IN
        ('OrderPlaced','OrderDelivering','OrderDelivered','LowStock',
         'TaskAssigned','TaskCompleted','System'))
);

/* ---- 2.8 Sản xuất (UC-23, UC-26, UC-27, UC-28) --------------------- */
CREATE TABLE ProductionTasks (
    TaskId        INT IDENTITY(1,1) PRIMARY KEY,
    ProductId     INT            NOT NULL REFERENCES Products(ProductId),
    TargetQty     INT            NOT NULL,
    ActualYield   INT            NULL,                -- Baker nhập khi hoàn tất
    DefectQty     INT            NULL,                -- Baker nhập khi hoàn tất
    Status        VARCHAR(12)    NOT NULL DEFAULT 'Planned',
    CreatedBy     INT            NOT NULL REFERENCES Users(UserId),   -- Admin
    AssignedBaker INT            NULL REFERENCES Users(UserId),
    Deadline      DATETIME2      NULL,
    StartedAt     DATETIME2      NULL,
    CompletedAt   DATETIME2      NULL,
    Note          NVARCHAR(500)  NULL,
    CreatedAt     DATETIME2      NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_ProdTasks_Target CHECK (TargetQty > 0),
    CONSTRAINT CK_ProdTasks_Status CHECK (Status IN
        ('Planned','Assigned','InProgress','Completed','Cancelled')),
    CONSTRAINT CK_ProdTasks_Done CHECK (Status <> 'Completed'
        OR (ActualYield IS NOT NULL AND DefectQty IS NOT NULL)),
    CONSTRAINT CK_ProdTasks_Qty CHECK (COALESCE(ActualYield,0) >= 0 AND COALESCE(DefectQty,0) >= 0)
);

CREATE TABLE ProductionTaskIngredients (             -- nhu cầu nguyên liệu tính từ recipe
    TaskId       INT            NOT NULL REFERENCES ProductionTasks(TaskId),
    IngredientId INT            NOT NULL REFERENCES Ingredients(IngredientId),
    RequiredQty  DECIMAL(18,3)  NOT NULL,
    CONSTRAINT PK_ProdTaskIng PRIMARY KEY (TaskId, IngredientId),
    CONSTRAINT CK_ProdTaskIng_Qty CHECK (RequiredQty > 0)
);
GO

/* ---- 2.9 Index bổ trợ --------------------------------------------- */
CREATE INDEX IX_Orders_Customer     ON Orders(CustomerId);
CREATE INDEX IX_Orders_Status_Date  ON Orders(Status, CreatedAt);
CREATE INDEX IX_PO_Supplier_Status  ON PurchaseOrders(SupplierId, Status);
CREATE INDEX IX_GR_Po               ON GoodsReceipts(PoId);
CREATE INDEX IX_InvTxn_Ingredient   ON InventoryTransactions(IngredientId, CreatedAt);
CREATE INDEX IX_ProdTasks_Baker     ON ProductionTasks(AssignedBaker, Status);
CREATE INDEX IX_Notif_User          ON Notifications(UserId, IsRead);
GO

/* ---- 2.10 View ---------------------------------------------------- */
-- Cảnh báo tồn thấp (UC-19/24): CHỈ để hiển thị/thông báo, không chặn thao tác nào.
CREATE VIEW vw_LowStockIngredients AS
SELECT IngredientId, Name, Unit, StockQty, MinThreshold,
       MinThreshold - StockQty AS ShortageQty
FROM   Ingredients
WHERE  IsActive = 1 AND StockQty < MinThreshold;
GO

-- Tiến độ giao hàng của từng dòng PO: đã nhận đạt bao nhiêu, còn thiếu bao nhiêu.
CREATE VIEW vw_PurchaseOrderProgress AS
SELECT po.PoId, po.PoCode, po.Status AS PoStatus, poi.PoItemId,
       i.Name AS IngredientName, i.Unit, poi.OrderedQty,
       COALESCE(SUM(gri.ReceivedQty - gri.RejectedQty), 0) AS AcceptedQty,
       poi.OrderedQty - COALESCE(SUM(gri.ReceivedQty - gri.RejectedQty), 0) AS RemainingQty
FROM   PurchaseOrders po
JOIN   PurchaseOrderItems poi ON poi.PoId = po.PoId
JOIN   Ingredients i          ON i.IngredientId = poi.IngredientId
LEFT JOIN GoodsReceiptItems gri ON gri.PoItemId = poi.PoItemId
GROUP BY po.PoId, po.PoCode, po.Status, poi.PoItemId, i.Name, i.Unit, poi.OrderedQty;
GO

/* =====================================================================
   3. DỮ LIỆU MẪU
   ===================================================================== */

/* ---- 3.1 Tài khoản (mật khẩu tạm: Bakery@123) ---------------------- */
SET IDENTITY_INSERT Users ON;
INSERT INTO Users (UserId, Email, PasswordHash, FullName, Phone, Role, IsActive) VALUES
 (1, N'tindlbhe171497@fpt.edu.vn', '$2b$10$/1Dg.sV51TYnK9BZzeP79uoODC.qI6/3dJJRFIYwjvmiFCLxXpQ6a', N'Admin TinDLB',   NULL, 'Admin',    1),
 (2, N'thaitqhe171880@fpt.edu.vn', '$2b$10$CA9HfJvDYsRisu.vx8kcI.KX6WB/oTid5RhSmz.5YBD2DrCzKxrAu', N'Cashier ThaiPQ',  NULL, 'Cashier',  1),
 (3, N'datpmhe176115@fpt.edu.vn',  '$2b$10$ztcRD.sxtu8BHO.ztmtb/u4wCbSV3fInfkJJuU37dv3kzEvkWKRzC', N'Baker DatPM',    NULL, 'Baker',    1),
 -- Tài khoản khách hàng THỬ NGHIỆM (email/SĐT giả, dùng để test luồng đặt hàng online)
 (4, N'khach01@example.com',       '$2b$10$NatmxQJuSxe/vSJV3zovjOCEq.PMuPRRhS8.vE/vYysmQqMqNBtH.', N'Nguyễn Văn An',  '0900000004', 'Customer', 1),
 (5, N'khach02@example.com',       '$2b$10$M9c02sI.OTzscqeWY9bUteY6uqgYcSfMYVj050wULPI6.eA8PIQOS', N'Trần Thị Bình',  '0900000005', 'Customer', 1);
SET IDENTITY_INSERT Users OFF;
GO

/* ---- 3.2 Danh mục & sản phẩm (tương ứng 4 công thức) --------------- */
SET IDENTITY_INSERT Categories ON;
INSERT INTO Categories (CategoryId, Name, Description) VALUES
 (1, N'Bánh kem',  N'Bánh kem tươi các loại'),
 (2, N'Bánh mì',   N'Bánh mì ngọt và bánh mì tươi'),
 (3, N'Bánh ngọt', N'Bánh ngọt cá nhân, cupcake'),
 (4, N'Bánh quy',  N'Bánh quy và cookie');
SET IDENTITY_INSERT Categories OFF;

-- StockQty của thành phẩm là TỒN ĐẦU KỲ giả định (chưa có phiếu sản xuất nào trong seed)
SET IDENTITY_INSERT Products ON;
INSERT INTO Products (ProductId, CategoryId, Name, Description, ImageUrl, Price, StockQty) VALUES
 (1, 1, N'Bánh kem dâu tây', N'Bánh bông lan kem tươi phủ dâu tây, đường kính 20cm', NULL, 350000, 5),
 (2, 2, N'Bánh mì bơ sữa',   N'Bánh mì mềm thơm bơ sữa, bán theo cái',               NULL,  15000, 40),
 (3, 3, N'Cupcake socola',   N'Cupcake socola đậm vị cacao',                          NULL,  25000, 24),
 (4, 4, N'Bánh quy bơ',      N'Bánh quy bơ giòn tan, bán theo cái',                   NULL,   6000, 60);
SET IDENTITY_INSERT Products OFF;
GO

/* ---- 3.3 Nguyên liệu (StockQty = 0, sẽ được tính từ phiếu nhập ở 3.7) */
SET IDENTITY_INSERT Ingredients ON;
INSERT INTO Ingredients (IngredientId, Name, Unit, StockQty, MinThreshold) VALUES
 (1,  N'Bột mì đa dụng',         N'kg',  0,   10),
 (2,  N'Bột mì số 13',           N'kg',  0,   10),
 (3,  N'Đường trắng',            N'kg',  0,    5),
 (4,  N'Bơ lạt',                 N'kg',  0,    5),
 (5,  N'Trứng gà',               N'quả',0,   60),
 (6,  N'Sữa tươi không đường',   N'lít',0,    5),
 (7,  N'Kem tươi whipping',      N'lít',0,   15),   -- ngưỡng 15: sau seed còn 12 -> để test cảnh báo tồn thấp
 (8,  N'Men khô instant',        N'g',   0,  200),
 (9,  N'Muối',                   N'g',   0,  500),
 (10, N'Bột cacao',              N'g',   0,  500),
 (11, N'Dâu tây',                N'kg',  0,    2),
 (12, N'Tinh chất vani',         N'ml',  0,  100);
SET IDENTITY_INSERT Ingredients OFF;
GO

/* ---- 3.4 Công thức (định lượng cho 1 mẻ) --------------------------- */
SET IDENTITY_INSERT Recipes ON;
INSERT INTO Recipes (RecipeId, ProductId, YieldQty, Instructions) VALUES
 (1, 1,  1, N'Đánh trứng với đường, trộn bột và bơ, nướng 170°C 35 phút. Phết kem tươi đánh bông, trang trí dâu tây.'),
 (2, 2, 10, N'Ủ bột với men 60 phút, chia 10 phần, ủ lần hai 40 phút, nướng 180°C 18 phút.'),
 (3, 3, 12, N'Trộn khô (bột, cacao), trộn ướt (trứng, bơ, sữa, đường), rót khuôn, nướng 175°C 22 phút.'),
 (4, 4, 30, N'Trộn bơ với đường, thêm trứng và vani, trộn bột, tạo hình, nướng 170°C 14 phút.');
SET IDENTITY_INSERT Recipes OFF;

INSERT INTO RecipeItems (RecipeId, IngredientId, Quantity) VALUES
 -- Bánh kem dâu tây (1 cái)
 (1, 1,   0.150), (1, 3, 0.120), (1, 5, 5),     (1, 4, 0.050),
 (1, 7,   0.400), (1, 11, 0.300), (1, 12, 5),
 -- Bánh mì bơ sữa (10 cái)
 (2, 2,   0.500), (2, 3, 0.050), (2, 4, 0.050), (2, 5, 1),
 (2, 6,   0.200), (2, 8, 5),     (2, 9, 5),
 -- Cupcake socola (12 cái)
 (3, 1,   0.250), (3, 3, 0.150), (3, 4, 0.100), (3, 5, 3),
 (3, 10, 40),     (3, 6, 0.100),
 -- Bánh quy bơ (30 cái)
 (4, 1,   0.300), (4, 4, 0.150), (4, 3, 0.100), (4, 5, 1), (4, 12, 3);
GO

/* ---- 3.5 Nhà cung cấp (thông tin GIẢ, thay bằng dữ liệu thật khi có) */
SET IDENTITY_INSERT Suppliers ON;
INSERT INTO Suppliers (SupplierId, Name, ContactPerson, Phone, Email, Address) VALUES
 (1, N'Công ty TNHH Nguyên liệu bánh Thiên Hương', N'Lê Văn Hương', '0281000001', N'thienhuong@example.com', N'Khu công nghiệp A, Hà Nội'),
 (2, N'Cơ sở Bơ Sữa Đồng Xanh',                     N'Phạm Thị Lan',  '0281000002', N'dongxanh@example.com',   N'Mộc Châu, Sơn La'),
 (3, N'Công ty TNHH Thực phẩm Vườn Xanh',           N'Hoàng Minh Tú', '0281000003', N'vuonxanh@example.com',   N'Đà Lạt, Lâm Đồng');
SET IDENTITY_INSERT Suppliers OFF;
GO

/* ---- 3.6 Đơn đặt hàng Supplier đã nhập kho ------------------------- */
-- PO1: nhận đủ trong 1 đợt              -> Completed
-- PO2: giao thiếu trứng + kem bị từ chối -> PartiallyFulfilled (vẫn chờ đợt 2, cùng mã PO)
-- PO3: nhận đủ trong 1 đợt              -> Completed
SET IDENTITY_INSERT PurchaseOrders ON;
INSERT INTO PurchaseOrders (PoId, PoCode, SupplierId, CreatedBy, Status, OrderDate, ExpectedDate, Note) VALUES
 (1, 'PO-20260914-001', 1, 1, 'Completed',          '2026-09-14 09:00:00', '2026-09-16', N'Nhập bột, đường, muối, men định kỳ'),
 (2, 'PO-20260917-002', 2, 1, 'PartiallyFulfilled', '2026-09-17 09:30:00', '2026-09-19', N'Nhập bơ, trứng, sữa, kem tươi'),
 (3, 'PO-20260921-003', 3, 1, 'Completed',          '2026-09-21 10:00:00', '2026-09-23', N'Nhập dâu tây, cacao, vani');
SET IDENTITY_INSERT PurchaseOrders OFF;

SET IDENTITY_INSERT PurchaseOrderItems ON;
INSERT INTO PurchaseOrderItems (PoItemId, PoId, IngredientId, OrderedQty, UnitPrice) VALUES
 -- PO1
 (1,  1, 1,   50,   22000),
 (2,  1, 2,   40,   24000),
 (3,  1, 3,   30,   21000),
 (4,  1, 9,   2000,    10),
 (5,  1, 8,   1000,   180),
 -- PO2
 (6,  2, 4,   20,  210000),
 (7,  2, 5,   300,   3500),
 (8,  2, 6,   30,   32000),
 (9,  2, 7,   15,   95000),
 -- PO3
 (10, 3, 11,  10,  120000),
 (11, 3, 10,  2000,   260),
 (12, 3, 12,  500,    900);
SET IDENTITY_INSERT PurchaseOrderItems OFF;
GO

/* ---- 3.7 Phiếu nhận hàng + sổ kho + tồn kho ------------------------ */
SET IDENTITY_INSERT GoodsReceipts ON;
INSERT INTO GoodsReceipts (ReceiptId, PoId, ReceivedBy, ReceivedDate, PaymentStatus, PaidAt, Note) VALUES
 (1, 1, 1, '2026-09-16 10:30:00', 'Paid', '2026-09-16 11:00:00', N'Nhận đủ, đạt chất lượng'),
 (2, 2, 1, '2026-09-19 14:00:00', 'Paid', '2026-09-19 15:00:00', N'Đợt 1: trứng giao thiếu 120 quả, 3 lít kem tươi bị từ chối - chờ Supplier giao bù'),
 (3, 3, 1, '2026-09-23 09:15:00', 'Paid', '2026-09-23 10:00:00', N'Nhận đủ, đạt chất lượng');
SET IDENTITY_INSERT GoodsReceipts OFF;

SET IDENTITY_INSERT GoodsReceiptItems ON;
INSERT INTO GoodsReceiptItems (ReceiptItemId, ReceiptId, PoItemId, ReceivedQty, RejectedQty, RejectReason, ExpiryDate) VALUES
 -- Phiếu 1 (PO1)
 (1,  1, 1,   50,   0, NULL, '2027-03-01'),
 (2,  1, 2,   40,   0, NULL, '2027-03-01'),
 (3,  1, 3,   30,   0, NULL, '2028-09-01'),
 (4,  1, 4,   2000, 0, NULL, '2029-09-01'),
 (5,  1, 5,   1000, 0, NULL, '2027-06-01'),
 -- Phiếu 2 (PO2, đợt 1)
 (6,  2, 6,   20,   0, NULL, '2027-03-01'),
 (7,  2, 7,   180,  0, NULL, '2026-10-19'),          -- đặt 300, mới giao 180
 (8,  2, 8,   30,   0, NULL, '2026-10-05'),
 (9,  2, 9,   15,   3, N'Kem tươi sắp hết hạn (HSD còn dưới 3 ngày), yêu cầu Supplier giao bù', '2026-10-10'),
 -- Phiếu 3 (PO3)
 (10, 3, 10,  10,   0, NULL, '2026-10-02'),
 (11, 3, 11,  2000, 0, NULL, '2027-09-01'),
 (12, 3, 12,  500,  0, NULL, '2028-09-01');
SET IDENTITY_INSERT GoodsReceiptItems OFF;

-- Sổ kho: mỗi dòng nhận hàng ĐẠT ghi 1 giao dịch nhập (= ReceivedQty - RejectedQty)
INSERT INTO InventoryTransactions (IngredientId, TxnType, QtyChange, RefTable, RefId, Note, CreatedBy, CreatedAt)
SELECT poi.IngredientId, 'Receipt', gri.ReceivedQty - gri.RejectedQty,
       'GoodsReceipts', gr.ReceiptId, po.PoCode, gr.ReceivedBy, gr.ReceivedDate
FROM   GoodsReceiptItems gri
JOIN   GoodsReceipts gr       ON gr.ReceiptId = gri.ReceiptId
JOIN   PurchaseOrderItems poi ON poi.PoItemId = gri.PoItemId
JOIN   PurchaseOrders po      ON po.PoId      = poi.PoId
WHERE  gri.ReceivedQty - gri.RejectedQty > 0;

-- Tồn kho nguyên liệu = tổng sổ kho (đảm bảo nhất quán)
UPDATE Ingredients
SET    StockQty = COALESCE((SELECT SUM(t.QtyChange)
                            FROM InventoryTransactions t
                            WHERE t.IngredientId = Ingredients.IngredientId), 0);

-- Giá trị đặt của PO
UPDATE PurchaseOrders
SET    TotalAmount = COALESCE((SELECT SUM(poi.OrderedQty * poi.UnitPrice)
                               FROM PurchaseOrderItems poi
                               WHERE poi.PoId = PurchaseOrders.PoId), 0);

-- Giá trị thanh toán của từng phiếu = chỉ tính hàng đạt chất lượng
UPDATE GoodsReceipts
SET    TotalCost = COALESCE((SELECT SUM((gri.ReceivedQty - gri.RejectedQty) * poi.UnitPrice)
                             FROM GoodsReceiptItems gri
                             JOIN PurchaseOrderItems poi ON poi.PoItemId = gri.PoItemId
                             WHERE gri.ReceiptId = GoodsReceipts.ReceiptId), 0);
GO

/* =====================================================================
   4. KIỂM TRA NHANH SAU KHI CHẠY
   ===================================================================== */
SELECT UserId, Email, Role FROM Users ORDER BY UserId;
SELECT i.IngredientId, i.Name, i.Unit, i.StockQty, i.MinThreshold FROM Ingredients i ORDER BY i.IngredientId;
SELECT * FROM vw_LowStockIngredients;        -- kỳ vọng: Kem tươi whipping (12 < 15)
SELECT * FROM vw_PurchaseOrderProgress WHERE RemainingQty > 0;  -- kỳ vọng: PO2 còn 120 trứng + 3 lít kem
SELECT PoCode, Status, TotalAmount FROM PurchaseOrders;
SELECT ReceiptId, PoId, TotalCost, PaymentStatus FROM GoodsReceipts;
GO
