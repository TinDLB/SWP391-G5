# Software Requirements Specification (SRS)
## Bakery Management System (BMS)

**Version:** 3.0  
**Target platform:** ASP.NET Core 8 MVC, Razor Views, Entity Framework Core 8, SQL Server  
**Document status:** Updated baseline for Iteration 3 (TinDLB & ThaiTQ implementations)  
**Language:** English  

---

## 1. Change Log

| Date | Action | In charge | Change Description |
|---|:---:|---|---|
| 13/09/2026 | A | TinDLB | Initial SRS baseline creation. |
| 14/09/2026 | A | ThaiTQ | Add Cashier initial profile and ordering requirements. |
| 14/09/2026 | A | DatPM | Add Baker production management workflow. |
| 12/10/2026 | M | TinDLB | Updated schema, 24 tables, and permission matrix. |
| 09/11/2026 | M | TinDLB, ThaiTQ | **Iteration 3 Update:** Implemented Staff Account Management, Voucher Management, Production Task Edit/Reassign/Cancel, Production & Stock Alert Notifications, Admin Dashboard, Admin Order Monitoring, Revenue Report with CSV Export (TinDLB); Implemented Cashier Dashboard, POS Counter Order, Online Order Management & Lifecycle Transitions, Self-Delivery Staff Assignment, Invoices, Customer Notifications upon Cashier Order/Delivery updates (ThaiTQ). |

---

## 2. Overall System Description

### 2.1 Actors
1. **Admin / Shop Owner:** Back-office administrator managing products, recipes, raw material inventory, procurement, staff accounts, promotions, production planning, order auditing, and management reporting.
2. **Cashier:** Sales and fulfillment operator handling counter POS sales, online order lifecycle management, delivery staff assignment, and invoice generation.
3. **Baker:** Production kitchen staff receiving assigned baking tasks, recording ingredient usage, entering actual yield and defect quantities.
4. **Customer:** Registered member purchasing online, managing addresses, applying vouchers, tracking order fulfillment, and receiving real-time notifications.
5. **Guest:** Anonymous visitor browsing the product catalog.

*(Suppliers and Delivery Staff are operational data entities; they do not possess application user accounts).*

### 2.2 Role Permissions Matrix

| Capability / Entity | Admin | Cashier | Baker | Customer |
|---|:---:|:---:|:---:|:---:|
| Authentication & Personal Profile | Full | Full | Full | Full |
| Category & Product Management | Full | View | View | View active |
| Recipe & Ingredient Management | Full | No | View | No |
| Supplier & Purchase Orders | Full | No | No | No |
| Production Task Creation & Edit/Cancel | Full | No | No | No |
| Production Task Execution (Start, Complete, Yield) | No | No | Full | No |
| Staff Account Administration (Cashier, Baker) | Full | No | No | No |
| Voucher Management (Promotions) | Full | Apply | No | Apply |
| Order Monitoring (All Orders) | Full | Manage | No | Own orders |
| Online Order Status Lifecycle (Pack & Prepare) | View | Full | No | Cancel Pending |
| Delivery Staff Assignment | View | Full | No | View assigned |
| POS Counter Order (Cash only) | View | Full | No | No |
| Invoice Generation & HTML Download | View | Full | No | View through order |
| User Notifications | Full (Admin alerts) | No | Task alerts | Order & Delivery alerts |
| Revenue Report & CSV Export Download | Full | No | No | No |
| Cashier Dashboard | No | Full | No | No |

---

## 3. Screen Inventory

| # | Screen Name | Path | Actor | Description |
|---|---|---|---|---|
| 1 | Sign In | `/Account/Login` | All | User authentication with cookie session. |
| 2 | Register | `/Account/Register` | Guest | Customer account registration with OTP validation. |
| 3 | Staff Management | `/Staff/Index` | Admin | Lists staff accounts with role, contact info, and active toggle. |
| 4 | Create / Edit Staff | `/Staff/Create`, `/Staff/Edit` | Admin | Staff form for Cashier and Baker accounts. |
| 5 | Voucher Management | `/Voucher/Index` | Admin | Lists vouchers with code, discount rules, validity, and toggle. |
| 6 | Create / Edit Voucher | `/Voucher/Create`, `/Voucher/Edit` | Admin | Form to define discount rules and constraints. |
| 7 | Edit Production Task | `/ProductionTask/Edit` | Admin | Adjust planned quantity, reassign Baker, or cancel task. |
| 8 | Admin Notifications | `/Admin/Notifications` | Admin | Centralized notification inbox with unread filtering. |
| 9 | Admin Orders | `/Admin/Orders` | Admin | Global order monitoring with channel, status, and date filters. |
| 10 | Admin Dashboard | `/Admin/Dashboard` | Admin | Operational KPI cards, stock warnings, and recent orders. |
| 11 | Admin Report & CSV Export | `/Admin/Report`, `/Admin/ExportReport` | Admin | Revenue report by date range with single-click CSV download. |
| 12 | Cashier Dashboard | `/Cashier/Dashboard` | Cashier | Today's revenue and status counts (Pending, Preparing, Delivering). |
| 13 | POS Counter Order | `/Cashier/CounterOrder` | Cashier | Direct walk-in sale, voucher application, and cash checkout. |
| 14 | Cashier Online Orders | `/Cashier/Orders` | Cashier | Order fulfillment grid with tabbed status filter. |
| 15 | Order Details & Fulfillment | `/Cashier/OrderDetails` | Cashier | Order inspection, lifecycle status actions, delivery assignment. |
| 16 | Assign Delivery Staff | `/Cashier/AssignDelivery` | Cashier | Form to record delivery staff name and phone number. |
| 17 | Sales Invoice | `/Cashier/Invoice`, `/Cashier/DownloadInvoice` | Cashier | Printable sales document and downloadable HTML invoice. |
| 18 | Baker Tasks | `/Baker/Index`, `/Baker/TaskDetail` | Baker | Assigned production tasks and recipe details. |
| 19 | Customer Order History | `/Order/Index`, `/Order/Detail` | Customer | Order status tracking with delivery staff information. |
| 20 | Customer Notifications | `/Notification/Index` | Customer | Personal alerts on order status updates and delivery assignment. |

---

## 4. Functional Requirements - Detailed Use Case Specifications

### 4.1 Cashier Sales & Fulfillment (ThaiTQ)

#### UC-42: Cashier Dashboard
- **Actor:** Cashier
- **Description:** Displays today's operational summary: completed sales revenue, pending orders count, preparing orders count, and delivering orders count.
- **Normal Flow:**
  1. Cashier logs in and opens `/Cashier/Dashboard`.
  2. System queries orders created today (`CreatedAt >= Today`).
  3. System renders KPI summary cards and quick navigation buttons to POS or Online Orders.

#### UC-43: POS / Counter Order
- **Actor:** Cashier
- **Description:** Allows Cashier to process purchases for in-store customers with instant cash payment and automatic invoice generation.
- **Rules:**
  - Payment method is strictly restricted to **Cash (Tiền mặt)**.
  - Decreases product stock immediately.
  - Automatically sets `Status = Completed` and `PaymentStatus = Paid`.
  - Automatically generates an `Invoice` record.

#### UC-45: Order Status Lifecycle Update & Customer Notification
- **Actor:** Cashier
- **Description:** Advances online orders through the fulfillment pipeline and triggers automated notifications to the customer.
- **Allowed Transitions:**
  `Pending -> Preparing -> Delivering -> Completed` (or `Cancelled`).
- **Notification Behavior:**
  - System invokes `_notificationService.NotifyAdminsAsync` for administrative auditing.
  - If `order.CustomerId != null`, system invokes `_notificationService.CreateNotificationAsync`:
    - **Title:** `Đơn hàng [OrderCode] đã được cập nhật`
    - **Message:** `Đơn hàng của bạn hiện ở trạng thái: [Trạng thái tiếng Việt]`
  - Customer receives notification immediately in `/Notification/Index` and can track progress in real time.

#### UC-46: Delivery Staff Assignment & Customer Notification
- **Actor:** Cashier
- **Description:** Assigns internal delivery personnel (name and 10-digit phone number) to online orders in `Delivering` status and sends notification to the customer.
- **Rules:**
  - Order must be in `Delivering` status.
  - Saves `DeliveryStaffName` and `DeliveryStaffPhone` to `Orders` table.
  - If `order.CustomerId != null`, system invokes `_notificationService.CreateNotificationAsync`:
    - **Title:** `Đơn hàng [OrderCode] đã có người giao hàng`
    - **Message:** `Người giao hàng: [DeliveryStaffName]. Số điện thoại: [DeliveryStaffPhone].`
  - Assigned delivery details are visible to Cashier, Customer order tracking, and the printed/downloaded invoice.

#### UC-47: Invoice Generation & HTML Export Download
- **Actor:** Cashier
- **Description:** Generates official sales invoice for completed paid orders and provides direct in-browser viewing, printing, and file download as HTML.
- **Content:** Store information, unique `InvoiceNo`, order code, recipient name, phone, shipping address, payment method, payment status, delivery staff name/phone, line items, and totals.
- **Download Action:** `/Cashier/DownloadInvoice/[id]` outputs a UTF-8 encoded HTML file with Content-Disposition attachment.

---

### 4.2 Admin Operations, Production & Reporting (TinDLB)

#### UC-35: Staff Account Management
- **Actor:** Admin
- **Description:** Manage Cashier and Baker staff accounts (FullName, Email, Phone, Role, Password, IsActive).
- **Rules:**
  - Email must be unique.
  - Password stored as BCrypt hash.
  - Inactive staff cannot log in or be assigned production tasks.
  - Admin accounts cannot be deactivated through this interface.

#### UC-13: Voucher Management
- **Actor:** Admin
- **Description:** Configure discount promotion codes with validation constraints: discount percentage or fixed amount, minimum order value, maximum discount cap, validity date range, and usage limits.

#### UC-37: Production Task Management (Edit / Reassign / Cancel)
- **Actor:** Admin
- **Description:** Adjust planned production quantities, reassign active bakers, or cancel pending production tasks.
- **Rules:**
  - Verifies available raw ingredient inventory against recipe requirements.
  - Dispatches `TaskUpdated` or `TaskCancelled` notifications to assigned Baker.

#### UC-38 & UC-24: Production & Low-Stock Notifications
- **Actor:** System / Admin / Baker
- **Description:** Automated event-driven notification dispatch.
  - Low-stock alerts (`LowStock`) sent to Admin when ingredient stock drops below `MinStockLevel`.
  - Production task assignments, updates, and cancellations notified to Bakers.
  - Task completion and start events notified to Admins.

#### UC-40: Admin Order Monitoring
- **Actor:** Admin
- **Description:** Global monitoring interface displaying all online and offline orders with filters for channel, fulfillment status, payment status, and date range.

#### UC-39: Admin Dashboard
- **Actor:** Admin
- **Description:** High-level executive overview with KPI summary cards (revenue, order counts, active users), low-stock warning banners, and recent order listings.

#### UC-41: Revenue Report & CSV Export Download (Tải báo cáo của admin)
- **Actor:** Admin
- **Description:** Filter orders and financial statistics by custom date range (`From` to `To`) and export the full dataset as a downloadable CSV file.
- **KPI Metrics:** Total order count, completed orders count, cancelled orders count, new registered users, and total paid revenue.
- **CSV Download Feature:**
  - Endpoint: `/Admin/ExportReport?from=YYYY-MM-DD&to=YYYY-MM-DD`
  - Columns: `OrderCode,Channel,Status,PaymentStatus,TotalAmount,CreatedAt`
  - MIME type: `text/csv`, UTF-8 encoded with RFC-4180 double-quote escaping.
  - Filename format: `bms-report-[StartDate]-[EndDate].csv`.

---

## 5. Non-Functional Requirements & Security
- **Data Integrity:** EF Core Serializable transactions prevent race conditions during order status transitions and stock deductions.
- **Security:** Anti-forgery tokens validate all POST forms. BCrypt securely hashes passwords. Role authorization enforces strict endpoint protection.
- **XSS Prevention:** Invoices and exported reports HTML-encode all dynamic values.