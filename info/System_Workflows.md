# Bakery Management System - 6 Core Workflows

Tài liệu này đặc tả 6 luồng nghiệp vụ (workflow) cốt lõi của hệ thống, bao gồm các trường hợp rẽ nhánh (ngoại lệ, thành công/thất bại). Các biểu đồ được viết bằng chuẩn Mermaid, bạn có thể copy code này dán vào **[Mermaid Live Editor](https://mermaid.live/)** hoặc nhúng trực tiếp vào Notion/GitHub để xem ảnh.

---

## 1. Workflow: Khách hàng đặt hàng (Online Order)
**Actor:** Customer (User)
**Mô tả:** Khách hàng lướt web, chọn bánh, áp dụng mã giảm giá và thanh toán online hoặc nhận hàng trả tiền (COD).

```mermaid
flowchart TD
    Start([Bắt đầu]) --> Browse[Xem danh sách Sản phẩm]
    Browse --> AddCart[Thêm vào giỏ hàng]
    AddCart --> Checkout[Bấm Thanh toán]
    Checkout --> SelectContact[Chọn thông tin nhận hàng]
    SelectContact --> Voucher{Có dùng Voucher?}
    
    Voucher -- Có --> Validate[Kiểm tra tính hợp lệ]
    Validate -- Hết hạn / Không hợp lệ --> Voucher
    Validate -- Hợp lệ --> PayMode
    
    Voucher -- Không --> PayMode[Chọn PTTT: COD / Online]
    
    PayMode --> |COD| CreateOrder[Tạo đơn hàng: Pending]
    PayMode --> |Online| Gateway[Chuyển hướng Cổng thanh toán]
    
    Gateway --> CheckPay{Thanh toán thành công?}
    CheckPay -- Thành công --> CreateOrder
    CheckPay -- Thất bại --> Fail[Báo lỗi thanh toán]
    
    CreateOrder --> End([Kết thúc])
    Fail --> End
```

---

## 2. Workflow: Bán hàng tại quầy (Counter Sale / POS)
**Actor:** Cashier
**Mô tả:** Khách đến tận tiệm mua bánh. Cashier thao tác tạo đơn, thu tiền mặt và giao bánh ngay lập tức (không có khâu giao hàng).

```mermaid
flowchart TD
    Start([Bắt đầu]) --> Create[Cashier tạo đơn mới]
    Create --> AddItem[Thêm Bánh vào đơn]
    AddItem --> CheckStock{Kho có đủ bánh?}
    
    CheckStock -- Không đủ --> Inform[Báo khách đổi món] --> Create
    CheckStock -- Đủ --> Voucher{Khách có Voucher?}
    
    Voucher -- Có --> Validate[Kiểm tra hệ thống]
    Validate -- Không hợp lệ --> Voucher
    Validate -- Hợp lệ --> Pay
    
    Voucher -- Không --> Pay[Thu tiền khách đưa]
    
    Pay --> Complete[Đổi trạng thái: Completed]
    Complete --> Deduct[Trừ tồn kho Sản phẩm]
    Deduct --> Print[In Hóa đơn] --> End([Kết thúc])
```

---

## 3. Workflow: Xử lý & Giao đơn Online (Order Fulfillment)
**Actor:** Cashier, Shipper
**Mô tả:** Nhận đơn online từ khách, đóng gói, bàn giao cho Shipper và theo dõi đến khi giao thành công.

```mermaid
flowchart TD
    Start([Bắt đầu]) --> View[Xem danh sách đơn Pending]
    View --> Pack[Lấy bánh & Đóng gói]
    Pack --> StatusPrep[Đổi trạng thái: Preparing]
    StatusPrep --> Assign[Gọi Shipper đến lấy]
    Assign --> InputShipper[Nhập tên, SĐT Shipper vào đơn]
    InputShipper --> StatusDel[Đổi trạng thái: Delivering]
    
    StatusDel --> CheckDel{Shipper giao thành công?}
    CheckDel -- Thành công --> StatusComp[Đổi trạng thái: Completed]
    CheckDel -- Khách bom hàng / Lỗi --> StatusCancel[Đổi trạng thái: Cancelled]
    
    StatusCancel --> Restock[Cộng lại Tồn kho Sản phẩm]
    
    StatusComp --> End([Kết thúc])
    Restock --> End
```

---

## 4. Workflow: Nhập kho nguyên liệu (Goods Receipt / Procurement)
**Actor:** Admin
**Mô tả:** Admin mua nguyên liệu (bột, bơ, sữa) từ Nhà cung cấp (Supplier) để phục vụ sản xuất.

```mermaid
flowchart TD
    Start([Bắt đầu]) --> Create[Tạo Phiếu nhập kho mới]
    Create --> SelectSup[Chọn Nhà cung cấp - Supplier]
    SelectSup --> AddIng[Nhập Nguyên liệu, Số lượng, Đơn giá]
    AddIng --> Confirm[Chốt tổng tiền thanh toán]
    Confirm --> Pay[Ghi nhận thanh toán cho Supplier]
    Pay --> UpdateStock[Cộng thêm Tồn kho Nguyên liệu]
    UpdateStock --> End([Kết thúc])
```

---

## 5. Workflow: Lên kế hoạch sản xuất (Production Planning)
**Actor:** Admin
**Mô tả:** Admin thấy bánh trong kho sắp hết, bèn tạo nhiệm vụ (Task) bắt Baker đi làm thêm bánh.

```mermaid
flowchart TD
    Start([Bắt đầu]) --> CheckStock[Kiểm tra lượng bánh tồn kho]
    CheckStock --> NeedProd{Cần làm thêm bánh?}
    NeedProd -- Không --> End([Kết thúc])
    NeedProd -- Cần --> CreateTask[Tạo Production Task]
    
    CreateTask --> Calc[Hệ thống tự tính Nguyên liệu cần dùng dựa vào Recipe]
    Calc --> CheckIng{Nguyên liệu kho có đủ không?}
    
    CheckIng -- Không đủ --> Warn[Cảnh báo: Phải nhập kho] --> End
    CheckIng -- Đủ --> Assign[Giao Task cho Baker]
    Assign --> StatusAssigned[Đổi trạng thái Task: Assigned] --> End
```

---

## 6. Workflow: Thực thi sản xuất (Production Execution)
**Actor:** Baker
**Mô tả:** Baker nhận lệnh từ Admin, tiến hành nướng bánh và báo cáo kết quả sau khi nướng xong.

```mermaid
flowchart TD
    Start([Bắt đầu]) --> ViewTasks[Baker xem danh sách Task được giao]
    ViewTasks --> StartTask[Bấm Bắt đầu làm]
    StartTask --> StatusProg[Đổi trạng thái Task: In Progress]
    StatusProg --> DeductIng[Hệ thống tự trừ Nguyên liệu tương ứng khỏi kho]
    
    DeductIng --> Bake[Quá trình trộn bột, nướng bánh]
    Bake --> Finish[Bấm Hoàn thành]
    Finish --> InputYield[Nhập số lượng bánh THỰC TẾ làm ra]
    InputYield --> StatusComp[Đổi trạng thái Task: Completed]
    
    StatusComp --> AddProd[Cộng số bánh vừa làm vào Tồn kho Sản phẩm]
    AddProd --> End([Kết thúc])
```
