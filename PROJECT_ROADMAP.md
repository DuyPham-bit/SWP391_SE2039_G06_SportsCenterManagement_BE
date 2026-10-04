# 📌 BẢNG THEO DÕI TIẾN ĐỘ & BỘ NHỚ DỰ ÁN (PROJECT ROADMAP & TASK TRACKER)

> **Dự án:** Hệ thống Quản lý Trung tâm Thể thao (Sports Center Management System - SCMS) — SWP391 (SE2039_G06)  
> **Kiến trúc:** .NET 10 / ASP.NET Core Web API — Kiến trúc 3 tầng chuẩn (API ➔ BLL ➔ DAL) kết hợp Repository & Unit of Work Pattern + SQL Server LocalDB.  
> **Mục đích file này:** Lưu trữ bộ nhớ liên tục (Persistent Memory) cho trợ lý AI và nhóm phát triển. Mỗi khi bắt đầu phiên làm việc mới, AI sẽ đọc file này để nắm ngay trạng thái công việc mà không bị quên bối cảnh.

---

## 👥 1. BẢNG PHÂN CÔNG VAI TRÒ NHÓM

| Thành viên | Phụ trách Module | Các chức năng chính |
| :--- | :--- | :--- |
| **Duy** | **Account, Member & Membership** | Đăng ký/đăng nhập (JWT Auth), Hồ sơ thành viên, Quản lý danh sách thành viên, Gói tập (`MembershipPackages`) và `MemberSubscription`. |
| **Huy** | **Class, Schedule & Coaching** | Bộ môn/Lớp/Phòng/Lịch tập, Phân công Coach, Ghi danh (`ClassEnrollment`), Điểm danh (`Attendance`), Kế hoạch tập luyện (`TrainingPlan`) & AI gợi ý bài tập. |
| **Thịnh** | **Reception, Payment, Reports & Integration** | **Integration Owner** (Quản lý DbContext, Migration, Program.cs); Thanh toán (`Payments` - VNPay/VietQR/MoMo), Check-in tại quầy, Báo cáo doanh thu (`Reports`), Hỗ trợ (`SupportRequests`), Thông báo (`Notifications`). |

---

## 🚀 2. TIẾN ĐỘ THỰC HIỆN TÍNH NĂNG (FEATURE STATUS)

### ✅ ĐÃ HOÀN THÀNH (DONE)
- [x] **Kiến trúc 3 tầng chuẩn (.NET 10):**
  - `SportsCenterManagement.API` (Controllers, Middleware, Swagger UI).
  - `SportsCenterManagement.BLL` (Business Logic Services, DTOs, Helpers độc lập).
  - `SportsCenterManagement.DAL` (DbContext, 41 Entities tách riêng, Repositories, Unit of Work).
- [x] **Cổng thanh toán VNPay Sandbox (Gateway Wrapper Pattern & SOLID):**
  - **Crypto & URL Wrapper (`VnPayLibrary.cs`):** Đóng gói logic sắp xếp Alphabet A-Z (`SortedList`), URL encoding và băm bảo mật HMAC-SHA512.
  - **Gateway Service Wrapper (`IVnPayService` ➔ `VnPayService`):** Bọc toàn bộ giao thức tạo link và phân tích chữ ký phản hồi của VNPay thành DTO nội bộ chuẩn.
  - **Core Orchestrator (`IPaymentService` ➔ `PaymentService`):** Điều phối nghiệp vụ trung tâm (kiểm tra Member, tạo Invoice, Subscription, ghi nhận Payment qua `IUnitOfWork`).
  - Cấu hình `TmnCode` (`987L6F2Z`) & `HashSecret` chính chủ hoạt động 100%.
  - API tạo URL: `POST /api/payments/create-vnpay-url`.
  - API nhận Callback / IPN: `GET /api/payments/vnpay-callback`.
  - Đã test thành công trên thẻ test NCB Sandbox.

- [x] **Cổng thanh toán Ví điện tử MoMo Sandbox (Gateway Wrapper Pattern & API v2):**
  - **Crypto Wrapper (`MomoSecurity.cs`):** Bọc toàn bộ thuật toán băm HMAC-SHA256, chuẩn hóa format raw signature chuẩn MoMo API v2, xác thực chữ ký phản hồi chống giả mạo.
  - **Gateway Service Wrapper (`IMoMoService` ➔ `MoMoService`):** Bọc giao tiếp HTTP RESTful Server-to-Server (`HttpClient`), gửi JSON sang MoMo API và parse kết quả `payUrl`, `deeplink`, `qrCodeUrl`.
  - **Core Orchestrator (`IPaymentService` ➔ `PaymentService`):** Điều phối tạo Subscription, Invoice và tự động kích hoạt gói tập (`Paid`/`Active`) khi MoMo callback/IPN thành công.
  - Cấu hình chuẩn Sandbox trong `appsettings.json`.
  - API tạo link: `POST /api/payments/create-momo-url`.
  - API nhận Callback trình duyệt: `GET /api/payments/momo-callback`.
  - API nhận Webhook Server-to-Server: `POST /api/payments/momo-ipn`.
  - Đã test thành công trên Swagger và giao diện Sandbox MoMo.

- [x] **Cổng thanh toán VietQR / Chuyển khoản Napas 247 (PayOS Gateway Wrapper & Webhook):**
  - **Crypto & Webhook Signature Wrapper (`PayOsSecurity.cs`):** Tự động sắp xếp Alphabet A-Z các trường dữ liệu, băm chữ ký HMAC-SHA256 chuẩn PayOS, xác thực Webhook Server-to-Server chống giả mạo 100%.
  - **Gateway Service Wrapper (`IPayOsService` ➔ `PayOsService`):** Đóng gói giao tiếp RESTful API sang PayOS, tạo link `checkoutUrl` và sinh mã `qrCode` Napas 247.
  - **Core Orchestrator (`IPaymentService` ➔ `PaymentService`):** Tự động sinh `orderCode` số nguyên, khởi tạo `Invoice` (`SC-{orderCode}`) & `MemberSubscription` (`PendingPayment`), xử lý Webhook kích hoạt gói tập (`Paid`/`Active`) và lưu giao dịch `Payments` (`VIETQR-PAYOS`).
  - **API Endpoints:** `POST /api/payments/create-vietqr` và `POST /api/payments/payos-webhook`.
  - *Ghi chú:* Đã hoàn tất 100% logic mã nguồn Backend, bước đăng ký tài khoản `my.payos.vn` lấy ClientId/ApiKey thực tế sẽ điền vào `appsettings.json` sau.

- [x] **Đồng bộ 4 Gói tập chuẩn từ giao diện Frontend vào Database:**
  - `Gói Basic Thể Thao`: 650.000đ (30 ngày, 1 môn tự chọn).
  - `Gói Pro Bứt Phá`: 1.800.000đ (90 ngày, 3 môn tự chọn).
  - `Gói Elite Chuyên Nghiệp`: 3.200.000đ (180 ngày, 6 môn tự chọn).
  - `Gói All-Access Olympic Pass`: 5.800.000đ (365 ngày, 15 môn).
  - Cơ chế **UPSERT (`DbInitializer.cs`)** tự động đồng bộ khi chạy server mà không vi phạm Foreign Key.
- [x] **Xác thực thanh toán tại quầy & Hủy giao dịch nhầm (Counter Checkout & Void):**
  - **Core Service (`IPaymentService` ➔ `PaymentService`):**
    - `ProcessCounterPaymentAsync`: Xử lý thanh toán Tiền mặt (CASH) / Quẹt thẻ (POS) tại quầy, tự động tính tiền thối (`changeDue`), mở Transaction Atomic tạo `Invoice` (`Paid`), `Payment` (`Completed`, lưu `processed_by`), kích hoạt `MemberSubscription` (`Active`) ngay tức thì.
    - `VoidCounterPaymentAsync`: Cơ chế Hủy giao dịch nhầm (Grace period 15 phút), thu hồi gói tập (`Status = Cancelled`), chuyển hóa đơn sang `Cancelled`, đánh dấu giao dịch `Voided` và lưu Audit log lý do hủy.
  - **API Endpoints:**
    - `POST /api/payments/counter-checkout`: Tiếp nhận thanh toán quầy.
    - `POST /api/payments/counter-void/{invoiceNumber}`: Hủy giao dịch nhầm.
  - **Tài liệu ghép nối Frontend:** Đã xuất file [HUONG_DAN_GHEP_NOI_FE_COUNTER_PAYMENT.md](file:///d:/0.%20Desk%27/FPT_Terms/FPT_Fall_26/SWP391/SportCenter_Project/Code/SWP391_SE2039_G06_SportsCenterManagement_BE/HUONG_DAN_GHEP_NOI_FE_COUNTER_PAYMENT.md).

- [x] **Swagger UI & API Document:** Tích hợp Swagger tại `/swagger` để test trực quan không cần Postman.

---

### 🟡 ĐANG LÀM / CHỜ GHÉP (IN PROGRESS)
- [ ] **Ghép nối Frontend `CounterMembership.jsx`:** Kết nối API `counter-checkout` và in hóa đơn `window.print()`.
- [ ] **Đăng ký tài khoản PayOS (my.payos.vn) & Điền API Keys thực tế:** Cập nhật `ClientId`, `ApiKey`, `ChecksumKey` vào `appsettings.json`.
- [ ] **Kết nối Frontend `MemberPackages.jsx` ➔ Backend:**
  - Ghép API `paymentApi.createVnpayUrl` / `paymentApi.createMomoUrl` / `paymentApi.createVietQr` và điều hướng sang `paymentUrl` / hiển thị QR modal.
  - Trang nhận kết quả `PaymentResult.jsx` gọi callback API.
- [ ] **Xác thực người dùng (JWT Authentication & Claims):**
  - Gắn `memberId` thực tế từ JWT Token thay vì `X-Member-Id` header tạm thời.

---

### 📋 SẼ LÀM TIẾP THEO (TODO ROADMAP / BACKLOG)

#### 💳 Module Payments & Reception (Thịnh phụ trách):
1. **Báo cáo Doanh thu (`ReportsController`):** Thống kê doanh thu gói tập, doanh thu theo cơ sở (`center_id`), theo khoảng thời gian.
2. **Check-in tại quầy (`CheckinsController`):** Quét mã thành viên khi vào trung tâm.

#### 👤 Module Auth & Membership (Duy phụ trách):
1. `POST /api/auth/register`, `POST /api/auth/login` (JWT token).
2. `GET /api/members/me` (Thông tin cá nhân, gói tập hiện tại, ngày hết hạn).
3. Quản lý Member CRUD cho Receptionist/Manager.

#### 🏋️ Module Class, Schedule & Coaching (Huy phụ trách):
1. Quản lý lớp học (`ClassesController`), Lịch tập (`SchedulesController`).
2. Ghi danh vào lớp (`ClassEnrollment`) — Chỉ cho phép khi có Subscription `Active`.
3. Điểm danh (`Attendance`) & Đặt chỗ ca tập (`SessionBooking`).
4. Kế hoạch rèn luyện (`TrainingPlan`) & Tích hợp AI gợi ý bài tập.

---

## 🛠️ 3. HƯỚNG DẪN CHẠY VÀ TEST HÀNG NGÀY

```powershell
# 1. Điều hướng vào thư mục Backend
cd Backend

# 2. Khôi phục packages & database nếu cần
dotnet build SWP391_SE2039_G06_SportsCenterManagement1.slnx

# 3. Chạy server API
dotnet run --project SportsCenterManagement.API

# 4. Truy cập giao diện Swagger
# Mở trình duyệt: http://localhost:54162/swagger
```

---

## 🔒 4. QUY TẮC PHÁT TRIỂN & BẢO MẬT CẦN NHỚ
1. **Bảo mật giá tiền:** Backend luôn tự truy vấn giá gốc từ Database bảng `membership_packages`, không bao giờ tin tưởng giá tiền từ Frontend gửi lên.
2. **Kiến trúc 3 tầng sạch:** Tầng `BLL` là thư viện C# độc lập, không chứa `HttpContext` hay `IQueryCollection`. Tầng `API Controller` nhận request và chuyển đổi dữ liệu xuống.
3. **Quản lý Migration:** Tất cả thay đổi Database thực hiện qua `SportsCenterManagement.DAL` và do Integration Owner quản lý.
