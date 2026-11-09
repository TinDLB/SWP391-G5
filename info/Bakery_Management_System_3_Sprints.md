# Bakery Management System

## 1. Project Overview

The Bakery Management System is a web-based system for managing a small bakery shop.

The system supports:
- Product and category management
- Ingredient and inventory management (bao gồm Nhập kho / Goods Receipt)
- Recipe management
- Production planning and baker assignment
- Finished-product stock management
- **Sale Module**: Tích hợp Shopping Cart, Online Orders, Counter sales, Voucher và Loyalty Points.
- **Contact Management**: Quản lý thông tin chung cho cả Khách hàng (Cá nhân/Tổ chức) và Nhà cung cấp (Supplier).
- Delivery management (Có theo dõi thông tin Shipper).
- Payment and invoices
- Notifications
- Reports and dashboard

The system is designed for a small-shop business model where the shop owner/admin may also participate in baking.

---

## 2. Actors and Permissions

### 2.1 Actors

1. **Guest**: Khách vãng lai, chưa đăng nhập.
2. **User**: Người dùng đã có tài khoản (đại diện cho Customer đặt hàng online).
3. **Cashier**: Thu ngân.
4. **Baker**: Thợ làm bánh.
5. **Admin / Shop Owner**: Quản trị viên / Chủ cửa hàng.

*(Lưu ý: Supplier - Nhà cung cấp và Shipper - Người giao hàng chỉ là các thực thể dữ liệu lưu trong hệ thống, không có tài khoản đăng nhập).*

---

### 2.2 Guest
A Guest can browse the website without logging in.
- Browse Products, View Promotions, Register, Login.
- **Guest purchase rule**: Guest cannot add products to the cart. Must Login/Register first.

### 2.3 User (Customer Account)
A User is a logged-in account.
- Add products to cart, Update/Remove cart items.
- Checkout, Select pickup or delivery.
- Create / Select Contact information for the order (vd: đặt cho cá nhân, đặt cho công ty).
- Apply valid voucher.
- Make payment.
- View order history & order details, track delivery status.
- Earn and view **Loyalty points** (Điểm thưởng gắn liền với tài khoản User, không gắn với Contact).

---

## 3. Contact Management (Thay thế Customer Management)

Hệ thống quản lý thông tin giao dịch thông qua **Contact**. Contact dùng chung cho cả Khách hàng và Nhà cung cấp.

Phân loại Contact:
1. **Individual (Cá nhân)**: Tên, SĐT, Địa chỉ.
2. **Organization (Tổ chức / Công ty)**: Tên, SĐT, Địa chỉ, Tên công ty, Mã số thuế.

Contact Type:
- **Customer**: Lưu thông tin giao hàng/xuất hóa đơn của người mua.
- **Supplier**: Lưu thông tin của nhà cung cấp nguyên liệu.

Một tài khoản User có thể tạo và lưu nhiều Contact (VD: User A lưu Contact nhận hàng ở nhà, và Contact xuất hóa đơn cho Công ty X).

---

## 4. Product and Category Management

**Admin**: CRUD Category, CRUD Product, set price, image, description.
**Guest / User**: Browse, Search, Filter.

---

## 5. Ingredient and Supplier Management

**Admin**:
- CRUD Ingredient.
- View ingredient stock, set minimum stock level.
- **Manage Suppliers**: Thêm/Sửa/Xóa nhà cung cấp (Contact type = Supplier).
- **Goods Receipt (Nhập kho)**: Tạo phiếu nhập kho khi mua nguyên liệu. Chọn Supplier, nguyên liệu, số lượng, giá tiền.
  - Sau khi tạo, tồn kho nguyên liệu tăng lên, hệ thống ghi nhận chi phí (Expense).

---

## 6. Recipe Management
**Admin**: CRUD Recipe, set required quantity for each ingredient.
**Baker**: View Recipe (cannot edit).

---

## 7. Production Management
**Admin**: Creates Production Task -> Select Product -> Planned Quantity -> System calculates required Ingredients & checks Inventory -> Assign to Baker.
**(Insufficient Ingredient Logic)**: System adjusts max possible quantity if inventory is low.
**Baker**: Views Task -> Starts Production -> Completes Production -> Enters Actual Quantity Produced.
**System**: Deducts raw ingredients and increases finished-product stock based on Actual Quantity.

---

## 8. Sale Module (Cart, Order, Voucher, Loyalty)

### 8.1 Shopping Cart & Checkout
- **Online Order**: User adds to cart -> Checkout -> Select Contact (Delivery Info) -> Apply Voucher/Loyalty -> Payment -> Order Created.
- **Counter Order**: Cashier creates order -> Select Products -> Apply Voucher -> Payment -> Complete.

### 8.2 Voucher & Loyalty Points
- **Voucher**: Admin CRUD Vouchers (discount, usage limit, min order value).
- **Loyalty Point**: 
  - Điểm thưởng được tích lũy vào tài khoản **User** sau khi đơn hàng hoàn thành.

---

## 9. Delivery & Order Tracking

Hệ thống hỗ trợ giao hàng tận nơi hoặc khách tự đến lấy (Pickup).

**Luồng Giao Hàng (Delivery Logic):**
1. Đơn hàng chuyển sang trạng thái **Ready** (Đã chuẩn bị xong).
2. Cashier gọi người giao hàng (Shipper nội bộ hoặc gọi xe ngoài).
3. Cashier cập nhật **Tên Shipper** và **SĐT Shipper** vào đơn hàng trên hệ thống.
4. Đơn hàng chuyển sang trạng thái **Delivering**. 
5. Khi Cashier nhận được thông tin xác nhận khách đã nhận được hàng -> Cashier bấm hoàn tất -> Đơn hàng chuyển sang **Completed**.

---

## 10. Notifications
Important notifications for Admin:
- Low ingredient stock.
- Production issue.

---

## 11. Reports and Admin Dashboard
Admin dashboard can include:
- Daily/Monthly revenue.
- Best-selling products.
- Ingredient cost (từ các Phiếu nhập kho).
- Estimated profit = Revenue - Ingredient Cost.
- Planned vs actual production.

---

## 12. Suggested Database Entities

Core entities:
```text
User
Role
Contact (Type: Individual/Organization, Role: Customer/Supplier)
Category
Product
Ingredient
GoodsReceipt (Phiếu nhập kho)
GoodsReceiptDetail
Recipe
RecipeIngredient
ProductionTask
Order
OrderDetail
Payment
Invoice
Voucher
Notification
```

---

## 13. 3-Sprint Development Plan (Chia theo Actor cho 5 người)

### Sprint 1 - Foundation and Master Data
**Member 1 (User/Guest)**: Authentication (Login/Register), User profile, Hiển thị danh sách sản phẩm cho Guest.
**Member 2 (Cashier)**: Giao diện Cashier cơ bản, Cấu trúc giỏ hàng nội bộ (Counter Cart).
**Member 3 (Baker + Kho)**: Cấu trúc DB Kho, Quản lý Ingredient, Cấu hình Supplier (Contact).
**Member 4 (Admin - Master Data)**: DB Schema chung, Quản lý Category & Product, Quản lý Recipe.
**Member 5 (Admin - Manager)**: Cấu trúc Contact chung (Khách/Tổ chức), Cấu hình CRUD Voucher.

### Sprint 2 - Core Operations (Sản xuất & Bán hàng)
**Member 1 (User/Guest)**: Giỏ hàng Online, Checkout online, Áp dụng Voucher/Loyalty.
**Member 2 (Cashier)**: Tạo đơn tại quầy, Tiếp nhận đơn Online, Cập nhật trạng thái đơn (Ready, Delivering).
**Member 3 (Baker + Kho)**: Tạo Phiếu nhập kho (Goods Receipt), Xử lý logic trừ/cộng tồn kho, Giao diện Baker (nhận task, báo cáo hoàn thành).
**Member 4 (Admin - Master Data)**: Tính năng tạo Production Task, Phân công công việc cho Baker dựa trên Recipe.
**Member 5 (Admin - Manager)**: Quản lý Loyalty Points, Quản lý logic Delivery (Shipper tracking & hoàn tất đơn).

### Sprint 3 - Reports, Polish & Delivery
**Member 1 (User/Guest)**: Lịch sử đơn hàng của User, Tracking trạng thái đơn, UI/UX polish.
**Member 2 (Cashier)**: Xử lý in Hóa đơn (Invoice), Xử lý ngoại lệ (Hủy đơn/Hoàn tiền).
**Member 3 (Baker + Kho)**: Báo cáo chi phí nguyên liệu (Cost Report), Lịch sử nhập kho.
**Member 4 (Admin - Master Data)**: Lịch sử sản xuất, Cảnh báo tồn kho thấp (Low stock notification).
**Member 5 (Admin - Manager)**: Báo cáo doanh thu (Revenue Dashboard), Báo cáo lợi nhuận, Full-system QA.

---

## 14. Workflows cá nhân theo góc nhìn Actor

Để đáp ứng yêu cầu "Mỗi người 1 workflow", nhóm sẽ thuyết trình dựa trên hành trình (journey) của từng Actor tương ứng mà thành viên đó phụ trách code:

### 👤 Member 1 (Phụ trách Actor: Guest / User)
- **Workflow: Trải nghiệm khách hàng Online (E-commerce Journey)**
- **Luồng Demo:** Khách vãng lai xem bánh -> Đăng ký tài khoản -> Cập nhật Profile -> Bỏ bánh vào giỏ -> Checkout (áp dụng mã giảm giá/điểm thưởng) -> Theo dõi trạng thái đơn hàng từ lúc Pending đến Completed.

### 👤 Member 2 (Phụ trách Actor: Cashier)
- **Workflow: Vận hành Quầy thu ngân & Xử lý đơn hàng (Point of Sale & Order Fulfillment)**
- **Luồng Demo:** Cashier tạo đơn bán trực tiếp tại quầy -> In hóa đơn -> Tiếp nhận đơn Online đổ về -> Giao cho Shipper (nhập thông tin Shipper) -> Xác nhận khách đã nhận hàng (Completed).

### 👤 Member 3 (Phụ trách Actor: Baker + Phụ trách Kho)
- **Workflow: Vòng đời Nguyên liệu & Sản xuất thực tế (Inventory & Baker Execution)**
- **Luồng Demo:** Nhập kho nguyên liệu từ Supplier (tạo Goods Receipt) -> Chuyển sang vai trò Baker xem danh sách bánh cần làm hôm nay -> Bấm "Hoàn thành sản xuất" -> Hệ thống tự động trừ nguyên liệu thô và cộng bánh thành phẩm vào kho.

### 👤 Member 4 (Phụ trách Actor: Admin - Master Data)
- **Workflow: Setup cấu hình hệ thống & Kế hoạch sản xuất (System Setup & Production Planning)**
- **Luồng Demo:** Đăng nhập Admin -> Tạo Product mới -> Tạo Recipe (định mức nguyên liệu) -> Lập Kế hoạch sản xuất (Production Task) -> Phân công nhiệm vụ cho Baker -> Kiểm tra cảnh báo Low Stock.

### 👤 Member 5 (Phụ trách Actor: Admin - Manager)
- **Workflow: Quản lý Khách hàng & Thống kê Kinh doanh (CRM & Analytics)**
- **Luồng Demo:** Quản lý danh bạ Contact (Tổ chức/Cá nhân) -> Phát hành Voucher / Thiết lập hệ thống Loyalty -> Mở Dashboard xem biểu đồ Doanh thu (Revenue) -> Xem báo cáo Lợi nhuận (sau khi trừ chi phí nguyên liệu).
