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
- [x] **Cổng thanh toán VNPay Sandbox (Gói tập):**
  - Cấu hình `TmnCode` (`987L6F2Z`) & `HashSecret` chính chủ hoạt động 100%.
  - Helper `VnPayLibrary.cs` băm chữ ký chuẩn **HMAC-SHA512**, sort tham số A-Z, chuẩn hóa IP `127.0.0.1`.
  - API tạo URL: `POST /api/payments/create-vnpay-url`.
  - API nhận Callback / IPN: `GET /api/payments/vnpay-callback` (Cập nhật `Invoice` -> `Paid`, `MemberSubscription` -> `Active`, tạo `Payment`).
  - Đã test thành công trên thẻ test NCB Sandbox.
- [x] **Đồng bộ 4 Gói tập chuẩn từ giao diện Frontend vào Database:**
  - `Gói Basic Thể Thao`: 650.000đ (30 ngày, 1 môn tự chọn).
  - `Gói Pro Bứt Phá`: 1.800.000đ (90 ngày, 3 môn tự chọn).
  - `Gói Elite Chuyên Nghiệp`: 3.200.000đ (180 ngày, 6 môn tự chọn).
  - `Gói All-Access Olympic Pass`: 5.800.000đ (365 ngày, 15 môn).
  - Cơ chế **UPSERT (`DbInitializer.cs`)** tự động đồng bộ khi chạy server mà không vi phạm Foreign Key.
- [x] **Swagger UI & API Document:** Tích hợp Swagger tại `/swagger` để test trực quan không cần Postman.

---

### 🟡 ĐANG LÀM / CHỜ GHÉP (IN PROGRESS)
- [ ] **Kết nối Frontend `MemberPackages.jsx` ➔ Backend:**
  - Ghép API `paymentApi.createVnpayUrl` và điều hướng sang `paymentUrl`.
  - Trang nhận kết quả `PaymentResult.jsx` gọi `paymentApi.checkPaymentCallback`.
- [ ] **Xác thực người dùng (JWT Authentication & Claims):**
  - Gắn `memberId` thực tế từ JWT Token thay vì `X-Member-Id` header tạm thời.

---

### 📋 SẼ LÀM TIẾP THEO (TODO ROADMAP / BACKLOG)

#### 💳 Module Payments & Reception (Thịnh phụ trách):
1. **Thêm Cổng VietQR (PayOS / VietQR API):**
   - Sinh mã QR động chuẩn Napas 247 để member mở app ngân hàng (VCB, MB, Tech...) quét thanh toán trực tiếp.
2. **Thêm Cổng Ví điện tử MoMo Sandbox.**
3. **Thanh toán tại quầy (Cash Payment):** Tiếp nhận tiền mặt tại lễ tân, in hóa đơn và kích hoạt gói thủ công.
4. **Báo cáo Doanh thu (`ReportsController`):** Thống kê doanh thu gói tập, doanh thu theo cơ sở (`center_id`), theo khoảng thời gian.
5. **Check-in tại quầy (`CheckinsController`):** Quét mã thành viên khi vào trung tâm.

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

---

## 🎨 5. TIÊU CHUẨN THIẾT KẾ & FRONTEND (DESIGN-TASTE & FRONTEND-DESIGNER)
Hệ thống tích hợp 2 bộ quy chuẩn tại `.agents/skills/` và `.agents/rules/`:
1. **`design-taste` (Thẩm mỹ cao cấp):**
   - **Chống "AI Slop":** Không dùng gradient tím-hồng generic; radius tinh tế (`rounded-lg`, `rounded-xl`); border sắc nét (`border-black/5` hoặc `border-white/10`) thay vì bóng mờ xám đục.
   - **Typography & Rhythm:** Font sans hình học thể thao mạnh mẽ (Inter, Outfit, Plus Jakarta Sans); tương phản rõ rệt giữa Heading, Body và Metadata.
   - **Màu sắc 60-30-10:** 60% nền trung tính, 30% cấu trúc thẻ/khung, 10% accent dẫn mắt; tôn trọng khoảng trắng (white space).
2. **`frontend-designer` (Kỹ thuật Component):**
   - **Kiến trúc Atomic:** Layout ➔ Widgets ➔ Atoms. Tách biệt Custom Hooks (logic) và Presentational Components.
   - **Tailwind CSS & Motion:** Hạn chế arbitrary values; micro-interactions mượt mà bằng Framer Motion (spring physics); responsive chuẩn Mobile, Tablet, Desktop.

