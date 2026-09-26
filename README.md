# Bakery Management System (BMS) - SWP391 (Group G5)

Dự án Hệ thống Quản lý Tiệm Bánh được xây dựng bằng **ASP.NET Core MVC (.NET 10 / .NET 8)**.

## 1. Phân hệ hoàn thành: Actor Customer (Khách hàng)
- **Màn hình Đăng nhập (Login)**: Xác thực an toàn, Cookie Authentication, Remember Me, toggle ẩn/hiện mật khẩu, validate đầy đủ.
- **Màn hình Đăng ký (Register)**: Đăng ký tài khoản khách hàng mới, kiểm tra trùng lặp email, validate số điện thoại, mật khẩu và điều khoản tiệm bánh.
- **Phân quyền Role-based**: Hỗ trợ 4 Actor theo mô tả đề tài: `Customer`, `Cashier`, `Baker`, `Admin`.

## 2. Hướng dẫn chạy thử dự án (Local)

### Yêu cầu môi trường
- Đã cài đặt .NET SDK (.NET 8, 9 hoặc 10).
- Visual Studio 2022 / VS Code / Rider.

### Các bước chạy
1. Mở thư mục dự án trong Terminal:
   ```bash
   cd C:\Users\Admin\source\repos\SWP391-G5
   ```
2. Chạy lệnh:
   ```bash
   dotnet run
   ```
3. Truy cập vào đường dẫn hiển thị trên console (mặc định: `https://localhost:5001` hoặc `http://localhost:5000`).

## 3. Tài khoản kiểm thử có sẵn (Seed Data)
Mặc định hệ thống tự động sinh dữ liệu mẫu khi chạy lần đầu:
- **Khách hàng (Customer)**: `customer@bakery.com` / `123456`
- **Quản trị viên (Admin)**: `admin@bakery.com` / `123456`
- **Thu ngân (Cashier)**: `cashier@bakery.com` / `123456`
- **Thợ làm bánh (Baker)**: `baker@bakery.com` / `123456`

## 4. Cấu hình CSDL
Trong file `appsettings.json`:
- Mặc định `"DatabaseProvider": "Sqlite"` để chạy ngay lập tức mà không cần cài đặt SQL Server.
- Để chuyển sang **SQL Server** (chuẩn FPT), chỉ cần đổi:
  ```json
  "DatabaseProvider": "SqlServer"
  ```
  và cập nhật chuỗi kết nối `"SqlServerConnection"` phù hợp với máy của bạn.
