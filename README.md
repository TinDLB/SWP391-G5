# Bakery Management System (BMS) - SWP391 (Group G5)

Hệ thống Quản lý Tiệm Bánh (**Bakery Management System - BMS**) là ứng dụng web được xây dựng bằng **ASP.NET Core MVC** (.NET 10 / .NET 8) kết hợp **Entity Framework Core** và cơ sở dữ liệu **Microsoft SQL Server**.

---

## 1. Công nghệ sử dụng
- **Backend**: ASP.NET Core 10.0 MVC, C#
- **ORM / Database**: Entity Framework Core, Microsoft SQL Server
- **Bảo mật**: Cookie Authentication, Claims-based Authorization, mã hóa mật khẩu chuẩn BCrypt (`BCrypt.Net-Next`)
- **Frontend / UI**: Bootstrap 5, Bootstrap Icons (lưu trữ cục bộ), CSS3 (hỗ trợ View Transitions API mượt mà)

---

## 2. Các phân hệ và tính năng hiện tại

### 2.1. Phân hệ Xác thực & Người dùng (Authentication & Authorization)
- **Đăng nhập (Login)**: Xác thực Cookie bảo mật, hỗ trợ "Ghi nhớ đăng nhập" (Remember Me), toggle ẩn/hiện mật khẩu, kiểm tra trạng thái kích hoạt tài khoản (`IsActive`).
- **Đăng ký (Register)**: Cho phép khách hàng mới tạo tài khoản, kiểm tra trùng lặp email, ràng buộc mật khẩu và số điện thoại, tự động đăng nhập sau khi đăng ký.
- **Phân quyền Role-based**: Điều hướng chính xác theo vai trò:
  - `Admin`: Tự động chuyển hướng tới Admin Dashboard (`/Admin/Dashboard`).
  - `Customer`, `Cashier`, `Baker`: Đăng nhập an toàn theo quyền hạn tương ứng.
- **Đăng xuất (Logout)**: Hủy bỏ phiên làm việc và xóa cookie xác thực an toàn.

### 2.2. Phân hệ Quản trị (Admin)
- **Admin Dashboard**: Thống kê tổng quan số lượng sản phẩm, đơn hàng, người dùng, nguyên liệu.
- **Quản lý sản phẩm bánh (Products)**:
  - Xem danh sách bánh kèm hình ảnh, giá bán, số lượng tồn kho và trạng thái.
  - Lọc theo danh mục bánh (Category) và trạng thái (Đang bán / Tạm ngưng).
  - Tìm kiếm sản phẩm theo tên bánh.

### 2.3. Cơ sở dữ liệu (Database Schema)
- File script `BakeryManagementDB.sql` gồm 24 bảng dữ liệu hoàn chỉnh:
  - Quản lý tài khoản: `Users`, `Addresses`
  - Bánh & Công thức: `Products`, `Categories`, `Recipes`, `RecipeItems`
  - Quản lý kho: `Ingredients`, `InventoryTransactions`, `StockAdjustments`, `StockCounts`, `StockCountItems`
  - Nhập hàng: `Suppliers`, `PurchaseOrders`, `PurchaseOrderItems`, `GoodsReceipts`, `GoodsReceiptItems`
  - Đơn hàng & Hóa đơn: `Orders`, `OrderItems`, `Invoices`, `CartItems`, `Vouchers`, `Notifications`, `ProductionTasks`

---

## 3. Hướng dẫn cài đặt & Chạy dự án (Local)

### 3.1. Yêu cầu môi trường
- **.NET SDK**: Phiên bản .NET 8, 9 hoặc 10.
- **Cơ sở dữ liệu**: Microsoft SQL Server (SQL Server 2019/2022 hoặc SQL Server Express).
- **IDE**: Visual Studio 2022 / VS Code / JetBrains Rider.

### 3.2. Khởi tạo Cơ sở dữ liệu
1. Mở SQL Server Management Studio (SSMS) hoặc Azure Data Studio.
2. Mở file `BakeryManagementDB.sql` trong thư mục gốc của dự án và thực thi (`Execute`) để tạo cơ sở dữ liệu `BakeryManagementDB` cùng dữ liệu mẫu ban đầu.

### 3.3. Cấu hình chuỗi kết nối
Mở file `appsettings.json` (hoặc `appsettings.Development.json`) và chỉnh sửa `SqlServerConnection` phù hợp với máy của bạn:
- **Nếu dùng SQL Server Express (phổ biến)**:
  ```json
  "ConnectionStrings": {
    "SqlServerConnection": "Server=localhost\\SQLEXPRESS;Database=BakeryManagementDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
  }
  ```
- **Nếu dùng SQL Server Default Instance**:
  ```json
  "ConnectionStrings": {
    "SqlServerConnection": "Server=(local);Database=BakeryManagementDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
  }
  ```

### 3.4. Chạy dự án
1. Mở Terminal tại thư mục dự án và gõ lệnh:
   ```bash
   dotnet run
   ```
2. Mở trình duyệt và truy cập:
   - HTTP: `http://localhost:5259`
   - HTTPS: `https://localhost:7270`

---

## 4. Tài khoản kiểm thử có sẵn (Seed Data)

Tất cả các tài khoản mẫu dưới đây đều dùng chung mật khẩu: **`Bakery@123`**

| Vai trò (Role) | Họ và tên | Địa chỉ Email | Mật khẩu |
| :--- | :--- | :--- | :--- |
| **Admin** | Admin TinDLB | `tindlbhe171497@fpt.edu.vn` | `Bakery@123` |
| **Cashier** (Thu ngân) | Cashier ThaiPQ | `thaitqhe171880@fpt.edu.vn` | `Bakery@123` |
| **Baker** (Thợ bánh) | Baker DatPM | `datpmhe176115@fpt.edu.vn` | `Bakery@123` |
| **Customer** (Khách hàng 1) | Nguyễn Văn An | `khach01@example.com` | `Bakery@123` |
| **Customer** (Khách hàng 2) | Trần Thị Bình | `khach02@example.com` | `Bakery@123` |

> *Gợi ý: Trên giao diện Đăng nhập đã có sẵn hộp bấm điền tự động tài khoản mẫu để thuận tiện cho việc test.*

---

## 5. Quy định Git & Làm việc nhóm
- **Quy chuẩn đặt tên Commit**:
  ```text
  <loại_thay_đổi>(phạm_vi): mô tả ngắn gọn nội dung đã làm
  ```
  *Ví dụ:* `feat(auth): bổ sung đăng nhập google`, `fix(ui): sửa lỗi hiển thị bảng sản phẩm`
- **Lưu ý cấu hình**: **Không commit** các file `appsettings.json` hoặc `appsettings.Development.json` lên remote nếu có chứa chuỗi kết nối riêng biệt của máy cá nhân để tránh gây xung đột kết nối giữa các thành viên.
