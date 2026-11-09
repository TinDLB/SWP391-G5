# Software Design Specification (SDS)
## Bakery Management System (BMS)

**Version:** 3.0  
**Target platform:** ASP.NET Core 8 MVC, Razor Views, Entity Framework Core 8, SQL Server  
**Database:** `BakeryManagementDB`  
**Document status:** Updated Detailed Code Design for Iteration 3 (TinDLB & ThaiTQ implementations)  
**Language:** English  

---

## 1. Change Log

| Date | Action | In charge | Change Description |
|---|:---:|---|---|
| 01/10/2026 | A | TinDLB | Initial SDS baseline creation. |
| 12/10/2026 | M | TinDLB | Updated High Level Architecture and Database Design for 24 tables. |
| 09/11/2026 | M | TinDLB, ThaiTQ | **Iteration 3 Update:** Expanded architecture components; updated database constraints for Orders and Notifications; added complete Section II Detailed Code Design for Feature Set 1 (Cashier Sales, Order Fulfillment, Delivery Staff Assignment, Invoices & Customer Notifications by ThaiTQ) and Feature Set 2 (Admin Operations, Production Planning & Reassignment, Staff Management, Voucher Management, and Revenue Report with CSV Export by TinDLB) with Class Diagrams and Sequence Diagrams. |

---

## 2. High Level Architecture

BMS follows a layered ASP.NET Core MVC architecture:

```text
+-------------------------------------------------------------+
|                        Client Tier                          |
|         Web Browser / Razor Views / Bootstrap UI            |
+-------------------------------------------------------------+
                              | HTTP(S) / Anti-Forgery Token
                              v
+-------------------------------------------------------------+
|                      Presentation Tier                      |
|  MVC Controllers (Admin, Cashier, Baker, Order, Checkout)   |
|  Dedicated Form & Display ViewModels (DataAnnotations)      |
+-------------------------------------------------------------+
                              | Dependency Injection
                              v
+-------------------------------------------------------------+
|                      Application Tier                       |
|  Business Services: NotificationService, BakerService,      |
|  IngredientService, VoucherService, PasswordService         |
+-------------------------------------------------------------+
                              | Unit of Work / Transactions
                              v
+-------------------------------------------------------------+
|                      Data Access Tier                       |
|   BakeryManagementDbContext (Entity Framework Core 8)       |
+-------------------------------------------------------------+
                              | SQL Provider / TDS
                              v
+-------------------------------------------------------------+
|                          Data Tier                          |
|         Microsoft SQL Server 2022 (BakeryManagementDB)      |
+-------------------------------------------------------------+
```

### Key Controller Responsibilities

| Controller | Role | Key Responsibilities |
|---|---|---|
| `AdminController` | Admin | Dashboard KPIs, global order monitoring, notification inbox, revenue report, and CSV export. |
| `StaffController` | Admin | CRUD operations for Cashier and Baker accounts, BCrypt password hashing, active status toggle. |
| `ProductionTaskController` | Admin | Production planning, task creation, quantity modification, baker reassignment, and cancellation. |
| `VoucherController` | Admin | Promotion management, discount type/value configuration, usage limit enforcement. |
| `CashierController` | Cashier | Counter POS order creation, online order listing, status lifecycle updates, delivery staff assignment, invoice rendering and HTML download. |
| `OrderController` | Customer | Order history browsing, order details tracking with delivery staff info, order cancellation. |
| `CheckoutController` | Customer | Cart checkout, voucher application, loyalty point redemption, and order placement. |
| `NotificationController` | Customer | Customer notification inbox and mark-as-read actions. |
| `BakerController` | Baker | Task execution, recipe ingredient inspection, entering actual yield and defect quantities. |

---

## 3. Database Schema Updates

### 3.1 Orders Table
- `OrderId`: Primary Key, INT IDENTITY(1,1).
- `OrderCode`: VARCHAR(30), Unique, non-null.
- `Channel`: VARCHAR(10) ('Online' or 'Offline').
- `CustomerId`: INT, Nullable, Foreign Key to `Users(UserId)`.
- `CashierId`: INT, Nullable, Foreign Key to `Users(UserId)`.
- `AddressId`: INT, Nullable, Foreign Key to `Addresses(AddressId)`.
- `DeliveryStaffName`: NVARCHAR(100), Nullable (Assigned by Cashier when Delivering).
- `DeliveryStaffPhone`: VARCHAR(20), Nullable (Assigned by Cashier when Delivering).
- `Status`: VARCHAR(12) ('Pending', 'Preparing', 'Delivering', 'Completed', 'Cancelled').
- `PaymentMethod`: VARCHAR(10) ('Cash', 'COD', 'Online').
- `PaymentStatus`: VARCHAR(10) ('Unpaid', 'Paid', 'Failed', 'Refunded').
- `Subtotal`, `DiscountAmount`, `TotalAmount`: DECIMAL(18,2).
- `CreatedAt`, `CompletedAt`: DATETIME2.

### 3.2 Notifications Table
- `NotificationId`: Primary Key, INT IDENTITY(1,1).
- `UserId`: INT, Foreign Key to `Users(UserId)`.
- `NotificationType`: VARCHAR(30) (`OrderStatusChanged`, `DeliveryAssigned`, `ProductCreated`, `ProductUpdated`, `ProductDeleted`, `OrderCancelled`, `LowStock`, `TaskAssigned`, `TaskUpdated`, `TaskCancelled`, `TaskCompleted`).
- `Title`: NVARCHAR(150), Non-null.
- `Message`: NVARCHAR(500), Nullable.
- `IsRead`: BIT, Default 0.
- `CreatedAt`: DATETIME2, Default SYSDATETIME().

---

## 4. Detailed Code Design

### 4.1 Feature Set 1: Cashier Sales, Fulfillment, Invoicing & Customer Notification (ThaiTQ)

#### 4.1.1 Class Diagram
- **`CashierController`**:
  - `- _context: BakeryManagementDbContext`
  - `- _notificationService: INotificationService`
  - `- _logger: ILogger<CashierController>`
  - `+ Dashboard(): Task<IActionResult>`
  - `+ CounterOrder(): Task<IActionResult>`
  - `+ CounterOrder(CounterOrderViewModel): Task<IActionResult>`
  - `+ Orders(string, string, string, int): Task<IActionResult>`
  - `+ OrderDetails(int): Task<IActionResult>`
  - `+ UpdateOrderStatus(int, string): Task<IActionResult>`
  - `+ AssignDelivery(int): Task<IActionResult>`
  - `+ AssignDelivery(AssignDeliveryViewModel): Task<IActionResult>`
  - `+ GenerateInvoice(int): Task<IActionResult>`
  - `+ Invoice(int): Task<IActionResult>`
  - `+ DownloadInvoice(int): Task<IActionResult>`
  - `+ PrintInvoice(int): Task<IActionResult>`

- **`NotificationService`**:
  - Implements `INotificationService`
  - `+ NotifyAdminsAsync(type, title, message): Task`
  - `+ CreateNotificationAsync(userId, type, title, message): Task`

#### 4.1.2 Sequence Diagram: POS Counter Order Checkout
```text
Cashier          CashierController        DbContext            VoucherService        Database
   |                     |                    |                      |                  |
   |-- POST CounterOrder |                    |                      |                  |
   |-------------------->|                    |                      |                  |
   |                     |-- Validate Stock ->|                      |                  |
   |                     |                    |---------------------------------------->|
   |                     |-- Apply Voucher ------------------------->|                  |
   |                     |<-- Discount Amount -----------------------|                  |
   |                     |-- Begin Transaction----------------------------------------->|
   |                     |-- Decrement Stock ------------------------------------------>|
   |                     |-- Insert Order (Offline, Cash, Completed, Paid) ------------>|
   |                     |-- Insert OrderItems & Invoice ------------------------------>|
   |                     |-- Commit Transaction --------------------------------------->|
   |                     |                                                              |
   |<-- RedirectToInvoice|                                                              |
```

#### 4.1.3 Sequence Diagram: Online Order Lifecycle Update with Customer Notification
```text
Cashier          CashierController        DbContext        NotificationService       Customer Inbox
   |                     |                    |                    |                       |
   |-- UpdateStatus(id) -|                    |                    |                       |
   |-------------------->|                    |                    |                       |
   |                     |-- Begin Serializable Tx                 |                       |
   |                     |-- Verify Valid Transition               |                       |
   |                     |-- Update Order.Status ->|               |                       |
   |                     |-- Commit Tx ------>|                    |                       |
   |                     |                                         |                       |
   |                     |-- NotifyAdminsAsync('OrderStatusChanged')                       |
   |                     |---------------------------------------->|                       |
   |                     |                                         |                       |
   |                     |-- CreateNotificationAsync(order.CustomerId,                     |
   |                     |   'OrderStatusChanged', 'Đơn hàng cập nhật')                    |
   |                     |---------------------------------------->|                       |
   |                     |                                         |-- Persist & Deliver ->|
   |<-- Redirect / Refresh                                                                 |
```

#### 4.1.4 Sequence Diagram: Delivery Staff Assignment & Customer Notification
```text
Cashier          CashierController        DbContext        NotificationService       Customer
   |                     |                    |                    |                     |
   |-- POST AssignDelivery(name, phone)       |                    |                     |
   |-------------------->|                    |                    |                     |
   |                     |-- Validate Delivering Status            |                     |
   |                     |-- Save Staff Info->|                    |                     |
   |                     |                                         |                     |
   |                     |-- CreateNotificationAsync(order.CustomerId,                   |
   |                     |   'DeliveryAssigned', 'Người giao hàng: ' + name)             |
   |                     |---------------------------------------->|                     |
   |                     |                                         |-- Send to Customer >|
   |<-- Redirect OrderDetails                                                            |
```

#### 4.1.5 Sequence Diagram: Invoice Generation & HTML Export Download
```text
Cashier          CashierController        DbContext                 Browser
   |                     |                    |                        |
   |-- DownloadInvoice --|                    |                        |
   |-------------------->|                    |                        |
   |                     |-- Query Order, Items, Address, Invoice ---->|
   |                     |-- Construct Formatted UTF-8 HTML            |
   |                     |-- Encode all values (HtmlEncode)            |
   |                     |-- Return File(bytes, 'text/html', filename) |
   |                     |-------------------------------------------->|
   |                     |                                             |-- Triggers Download
```

---

### 4.2 Feature Set 2: Admin Operations, Production, Staff & Reporting (TinDLB)

#### 4.2.1 Class Diagram
- **`AdminController`**:
  - `+ Dashboard(): Task<IActionResult>`
  - `+ Orders(channel, status, from, to): Task<IActionResult>`
  - `+ Notifications(unreadOnly): Task<IActionResult>`
  - `+ Report(from, to): Task<IActionResult>`
  - `+ ExportReport(from, to): Task<IActionResult>`
- **`ProductionTaskController`**:
  - `+ Index(): Task<IActionResult>`
  - `+ Create(ProductionTaskViewModel): Task<IActionResult>`
  - `+ Edit(int, ProductionTaskViewModel): Task<IActionResult>`
  - `+ Cancel(int): Task<IActionResult>`
- **`StaffController`**:
  - `+ Index(): Task<IActionResult>`
  - `+ Create(StaffFormViewModel): Task<IActionResult>`
  - `+ Edit(int, StaffFormViewModel): Task<IActionResult>`
  - `+ ToggleStatus(int): Task<IActionResult>`
- **`VoucherController`**:
  - `+ Index(): Task<IActionResult>`
  - `+ Create(VoucherFormViewModel): Task<IActionResult>`
  - `+ Edit(int, VoucherFormViewModel): Task<IActionResult>`
  - `+ ToggleStatus(int): Task<IActionResult>`

#### 4.2.2 Sequence Diagram: Staff Account Creation & Deactivation
```text
Admin            StaffController          DbContext          NotificationService     SQL Database
  |                     |                     |                      |                    |
  |-- POST Create(model)|                     |                      |                    |
  |-------------------->|                     |                      |                    |
  |                     |-- Verify Unique Email                      |                    |
  |                     |-- Hash Password (BCrypt)                   |                    |
  |                     |-- Insert User (Cashier/Baker) ------------>|                    |
  |                     |-- SaveChangesAsync() ------------------------------------------>|
  |                     |-- NotifyAdminsAsync('StaffCreated') ------>|                    |
  |<-- RedirectToIndex -|                                                                 |
```

#### 4.2.3 Sequence Diagram: Production Task Management (Edit / Reassign / Cancel)
```text
Admin        ProductionTaskController     DbContext          NotificationService     Baker Inbox
  |                     |                     |                      |                    |
  |-- POST Edit(model) -|                     |                      |                    |
  |-------------------->|                     |                      |                    |
  |                     |-- Verify Status != Completed               |                    |
  |                     |-- Check Ingredient Stock Requirements      |                    |
  |                     |-- Update TargetQuantity & AssignedBakerId ->|                   |
  |                     |-- SaveChangesAsync() ------------------------------------------>|
  |                     |-- CreateNotificationAsync(bakerId, 'TaskUpdated') ------------->|
  |                     |-- NotifyAdminsAsync('TaskUpdated') ------->|                    |
  |<-- RedirectToIndex -|                                                                 |
```

#### 4.2.4 Sequence Diagram: Admin Revenue Report & CSV Export Download
```text
Admin             AdminController         DbContext                 Browser
  |                      |                    |                        |
  |-- GET Report(from,to)|                    |                        |
  |--------------------->|                    |                        |
  |                      |-- Query Orders & Users in Range             |
  |                      |-- Aggregate Revenue & Counts                |
  |<-- Return View(model)|                                             |
  |                                                                    |
  |-- GET ExportReport --|                                             |
  |--------------------->|                                             |
  |                      |-- Query Orders AsNoTracking() ------------->|
  |                      |-- Build CSV (StringBuilder)                 |
  |                      |-- Return File(bytes, 'text/csv', filename) ->|
  |                      |                                             |-- Triggers Download
```

---

## 5. Security and Transaction Boundaries
1. **Concurrency Control:** Serializable database transactions protect order fulfillment status transitions and prevent race conditions.
2. **Authorization:** Controller actions enforce explicit role checks: `[Authorize(Roles = "Admin")]`, `[Authorize(Roles = "Cashier")]`, and `[Authorize(Roles = "Baker")]`.
3. **Password Security:** All passwords hashed with BCrypt with unique cryptographic salts.
4. **Input Sanitization:** Exported CSV and HTML invoice files use strict RFC-4180 escaping and `WebUtility.HtmlEncode` to eliminate injection vulnerabilities.