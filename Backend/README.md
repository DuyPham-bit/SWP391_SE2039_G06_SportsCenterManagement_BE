# Sports Center Management — Backend

Backend cho hệ thống quản lý trung tâm thể thao. Repository hiện dùng ASP.NET Core Web API theo kiến trúc 3 tầng chuẩn (3-Layer Architecture), EF Core và SQL Server LocalDB.

## Stack và cấu trúc 3 tầng chuẩn (3-Layer Architecture)

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core 10 + SQL Server
- SQL Server LocalDB cho môi trường phát triển mặc định
- Mô hình 3 tầng (Presentation - Business Logic - Data Access) kết hợp Repository Pattern & Unit of Work

```text
.
├── SportsCenterManagement.API/       # Layer 1: Presentation Layer (PL)
│   ├── Controllers/                  # Controllers chỉ nhận request, gọi Service qua Interface và trả DTO/JSON
│   ├── Program.cs                    # Cấu hình DI (DbContext, UnitOfWork, Services), Middleware
│   └── appsettings.json
├── SportsCenterManagement.BLL/       # Layer 2: Business Logic Layer (BLL)
│   ├── DTOs/                         # Data Transfer Objects (Classes, MembershipPackages, CoreFlows)
│   ├── Interfaces/                   # Abstractions / Service Contracts (IClassService, IMembershipPackageService, ICoreFlowService)
│   └── Services/                     # Business logic implementations (ClassService, MembershipPackageService, CoreFlowService)
├── SportsCenterManagement.DAL/       # Layer 3: Data Access Layer (DAL)
│   ├── Context/                      # SportsCenterDbContext
│   ├── Entities/                     # Tách từng Entity ra file .cs riêng biệt (Role, User, ClassEntity, Invoice,...)
│   ├── Migrations/                   # EF Core migrations
│   └── Repositories/                 # Generic Repository & Unit of Work (IGenericRepository, IUnitOfWork,...)
├── database/                         # SQL schema sinh từ migrations
├── docs/requirements/core-flows.md   # Ba flow bắt buộc, cases và DB đề xuất
├── docs/requirements/team-implementation-plan.md # Gộp 4 role và chia task Duy/Huy/Thịnh
└── SWP391_SE2039_G06_SportsCenterManagement1.slnx
```

## Chạy local

Yêu cầu .NET SDK 10 và SQL Server LocalDB hoặc SQL Server tương thích.

Trước khi chạy các lệnh, cần cấu hình `Jwt__Key` riêng dài tối thiểu 32 byte (không có JWT secret mặc định trong source). Với database chưa có System Admin, cấu hình thêm ba biến `Bootstrap__SystemAdmin__Username`, `Bootstrap__SystemAdmin__Email`, `Bootstrap__SystemAdmin__Password` như mục **Bootstrap System Admin đầu tiên** bên dưới.

```powershell
dotnet restore .\SWP391_SE2039_G06_SportsCenterManagement1.slnx
dotnet tool restore
dotnet ef database update --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj
dotnet run --project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj
```

API được xây bằng ASP.NET Core MVC Controllers và trả JSON; không dùng Razor Views vì repository này là backend cho frontend riêng. Các route Flow 1 đã triển khai:

| Method | Route | Mục đích |
|---|---|---|
| GET | `/health` hoặc `/api/health` | Health check, trả `{ "status": "ok" }` |
| GET | `/api/centers/{centerId}/membership-packages` | Danh sách gói Active của center |
| GET | `/api/centers/{centerId}/classes` | Danh sách lớp Published của center |
| POST | `/api/auth/register` | Tạo Member, hash mật khẩu và profile trong một transaction |
| POST | `/api/auth/login` | Đăng nhập và nhận bearer token 1 giờ |
| GET/PATCH | `/api/members/me` | Xem/cập nhật hồ sơ của chính member |
| GET/POST | `/api/centers/{centerId}/members` | Receptionist/Manager tra cứu hoặc tạo member tại quầy |
| PATCH | `/api/centers/{centerId}/members/{memberId}` | Manager cập nhật hồ sơ Member trong center |
| PATCH | `/api/centers/{centerId}/members/{memberId}/status` | Manager kích hoạt/vô hiệu hóa tài khoản Member |
| GET/POST/PATCH | `/api/centers/{centerId}/staff` | Manager/System Admin tra cứu, tạo hoặc cập nhật Coach/Receptionist theo quyền và phạm vi center |
| POST | `/api/members/me/subscriptions` | Tạo subscription PendingPayment, invoice và invoice item |
| POST/GET | `/api/members/{memberId}/subscriptions` | Bán gói tại quầy hoặc xem lịch sử theo quyền |
| POST | `/api/centers/{centerId}/membership-packages` | Manager tạo gói |
| PATCH | `/api/membership-packages/{packageId}` | Manager cập nhật gói |
| POST | `/api/payments/create-vnpay-url` | Tạo hoặc tiếp tục thanh toán gói của Member |
| POST | `/api/payments/create-momo-url` | Tạo hoặc tiếp tục thanh toán MoMo |
| POST | `/api/payments/create-payos-url` | Tạo hoặc tiếp tục thanh toán PayOS |
| GET | `/api/payments/vnpay-callback` | Xác thực kết quả callback VNPay |
| POST | `/api/payments/payos-ipn` | Xác thực webhook PayOS và ghi nhận giao dịch hợp lệ |
| POST | `/api/invoices/{invoiceId}/payments` | Manager/Receptionist ghi nhận tiền mặt; chỉ kích hoạt khi đã thu đủ |
| GET | `/api/centers/{centerId}/audit-logs` | Manager xem audit của center được phân công |
| GET | `/api/admin/audit-logs` | System Admin xem audit toàn hệ thống |
| GET | `/api/admin/roles` | System Admin xem ma trận quyền |
| PUT | `/api/admin/roles/{roleId}/permissions` | System Admin thay ma trận quyền của role; quyền này luôn chỉ thuộc SystemAdmin |

MVP tắt email verification; mật khẩu cần ít nhất 6 ký tự gồm chữ hoa, chữ thường, số và ký tự đặc biệt. Token bearer dùng ASP.NET Core Data Protection, hết hạn sau 1 giờ; API kiểm tra lại trạng thái và role trên mỗi request. Subscription chỉ bắt đầu khi invoice được thanh toán đủ.

Manager/Receptionist cần có `StaffProfile` trạng thái `Active` tại center được giao. Manager tạo Coach/Receptionist trong center mình quản lý; System Admin có thể quản trị nhân sự theo quyền được cấp. Mọi endpoint nhạy cảm kiểm tra permission và center ở backend.

### Bootstrap System Admin đầu tiên

Role `SystemAdmin` được seed nhưng không có tài khoản/mật khẩu mặc định. Với database chưa có System Admin, cần cấu hình đủ ba biến môi trường trước khi chạy API; nếu thiếu, API dừng khởi động để tránh vận hành hệ thống không có quản trị viên. Mật khẩu phải dài ít nhất 6 ký tự và có chữ hoa, chữ thường, số, ký tự đặc biệt. Khi đã có System Admin, bootstrapper không tự đổi thông tin tài khoản.

```powershell
$env:Bootstrap__SystemAdmin__Username = "<username>"
$env:Bootstrap__SystemAdmin__Email = "<email>"
$env:Bootstrap__SystemAdmin__Password = "<strong-password>"
```

System Admin là role duy nhất được sửa ma trận quyền và xem audit toàn hệ thống. Không cấp `roles.permissions.manage` cho role khác; backend từ chối thao tác này kể cả khi client gọi trực tiếp API.

Yêu cầu tiền mặt gửi `IdempotencyKey` từ 16 đến 100 ký tự cùng số tiền. Retry phải dùng cùng key cho cùng invoice/amount; tái sử dụng key cho yêu cầu khác sẽ trả conflict. Callback VNPay đã ký được lưu cả khi thất bại; callback báo thành công nhưng không khớp trạng thái/số dư được ghi `ReviewRequired`, không tự cộng doanh thu hoặc kích hoạt subscription.

Khi chạy với environment `Development`, backend tạo center/gói mẫu và các tài khoản demo `admin`, `manager01`, `reception01`, `member01` để phát triển local. Những dữ liệu mẫu này không được tự tạo ở môi trường khác; môi trường triển khai cần được cấp dữ liệu trung tâm, gói tập và tài khoản qua quy trình quản trị riêng. Không dùng mật khẩu demo cho môi trường triển khai. Nếu database đã từng được khởi tạo ở `Development`, hãy xóa hoặc đổi mật khẩu tài khoản demo và rà lại center/gói mẫu trước khi tái sử dụng ở môi trường khác.

### Flow 3 — thanh toán và báo cáo

Các API Flow 3 yêu cầu JWT, ngoại trừ callback/IPN do cổng thanh toán gọi. Tài khoản đăng nhập qua `POST /api/auth/login`; role, user, member và center được lấy từ claims, không nhận qua `X-Member-Id` hoặc `X-Staff-Id`. Mọi lệnh ghi thanh toán cần header `Idempotency-Key`.

| Method | Route | Vai trò | Mục đích |
|---|---|---|---|
| POST | `/api/payments/create-vnpay-url` | Member | Tạo hoặc phát lại link VNPay |
| POST | `/api/payments/create-momo-url` | Member | Tạo hoặc phát lại link MoMo |
| POST | `/api/payments/create-payos-url` | Member | Tạo hoặc phát lại link PayOS |
| GET | `/api/payments/vnpay-callback`, `/api/payments/vnpay-ipn` | Cổng thanh toán | Xác nhận callback/IPN có chữ ký |
| GET/POST | `/api/payments/momo-callback`, `/api/payments/momo-ipn` | Cổng thanh toán | Xác nhận callback/IPN có chữ ký |
| POST | `/api/payments/payos-ipn` | PayOS | Xác thực chữ ký webhook; chỉ callback thành công đúng orderCode/số tiền mới ghi nhận thanh toán |
| POST | `/api/payments/counter-checkout` | Receptionist, Manager, Admin | Thu CASH/POS tại quầy; hỗ trợ thu một phần bằng tiền mặt |
| POST | `/api/invoices/{invoiceNumber}/payments` | Receptionist, Manager, Admin | Thu tiếp số dư bằng CASH/POS |
| GET | `/api/invoices/{invoiceNumber}` | Người có quyền trên hóa đơn | Tra cứu và xuất lại hóa đơn/biên nhận |
| POST | `/api/payments/{paymentId}/refunds` | Manager, Admin | Hoàn tiền và lưu ledger riêng |
| POST | `/api/payments/refunds/{refundId}/reconcile` | Manager, Admin | Chốt refund Pending sau khi đối soát |
| POST | `/api/payments/{gatewayReference}/reconcile` | Manager, Admin | Đối soát payment Pending với bằng chứng |
| GET | `/api/reports/revenue` | Manager, Admin | Tổng gộp/refund/ròng theo ngày hoặc tháng |
| GET | `/api/reports/membership` | Manager, Admin | Tổng thành viên, active và đăng ký mới theo kỳ |
| GET | `/api/reports/classes` | Manager, Admin | Tổng hợp trạng thái ghi danh lớp theo kỳ |

Manager chỉ xem và thao tác trong center gắn với JWT. Admin phải truyền `centerId` khi xem báo cáo. Payment/refund timeout được giữ `Pending`; Manager cần kiểm tra ở nhà cung cấp và ghi bằng chứng trước khi reconcile. `Reports:TimeZoneId` mặc định `Asia/Ho_Chi_Minh` (Windows dùng `SE Asia Standard Time`); cấu hình này xác định ranh giới ngày báo cáo và ngày bắt đầu subscription. `EndDate` là ngày sử dụng cuối cùng, nên gói `DurationDays=30` có đúng 30 ngày hiệu lực.

PayOS cần cấu hình URL webhook công khai trỏ tới `POST /api/payments/payos-ipn` trong kênh thanh toán PayOS trước khi nhận giao dịch. Return URL chỉ phục vụ điều hướng giao diện; backend chỉ kích hoạt subscription từ webhook có chữ ký hợp lệ. Hoàn tiền PayOS chưa được hỗ trợ tự động; giao dịch này cần xử lý theo quy trình đối soát hiện có.

Schema hiện chưa gắn `MemberProfile` trực tiếp với center. Báo cáo thành viên tính một hội viên thuộc center nếu họ có ít nhất một subscription ở center đó; `NewMembers` được tính theo thời điểm subscription đầu tiên tại center được tạo. Hồ sơ chưa từng mua subscription chưa được phân bổ vào center.

Cổng được in ra khi chạy ứng dụng.

## Cấu hình database

Connection string key là `ConnectionStrings:SportsCenter`. Môi trường Development dùng SQL Server LocalDB như cấu hình và lệnh bên dưới. `.env.example` chỉ là mẫu vì .NET không tự nạp file `.env`; đặt các biến cần thiết trong terminal, User Secrets hoặc cấu hình môi trường của nơi triển khai.

PowerShell, chỉ cho phiên terminal hiện tại:

```powershell
$env:ConnectionStrings__SportsCenter = "Server=(localdb)\MSSQLLocalDB;Database=SportsCenterManagement;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

Không commit thông tin đăng nhập hoặc secret thật. Dùng User Secrets hoặc environment variables ngoài máy local.

JWT cần secret riêng dài tối thiểu 32 byte. Cấu hình bắt buộc `Jwt__Key` trước khi chạy. Có thể tạo nhanh cho phiên PowerShell hiện tại:

```powershell
$jwtBytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Fill($jwtBytes)
$env:Jwt__Key = [Convert]::ToBase64String($jwtBytes)
$env:Jwt__Issuer = "SportsCenterManagement"
$env:Jwt__Audience = "SportsCenterManagement.FE"
$env:Cors__AllowedOrigins__0 = "http://localhost:5173"
```

Gateway secret được đọc từ biến môi trường/User Secrets: `VnPay__HashSecret`, `Momo__AccessKey`, `Momo__SecretKey`, `PayOS__ClientId`, `PayOS__ApiKey`, `PayOS__ChecksumKey`. Không cấu hình provider thì các API khác vẫn chạy; endpoint tạo link của provider đó trả `503` trước khi tạo payment attempt.

Để chạy PayOS local, lấy ba giá trị kết nối từ tài khoản PayOS rồi chạy tại thư mục `Backend/Backend/SportsCenterManagement.API`:

```powershell
dotnet user-secrets set "PayOS:ClientId" "GIÁ_TRỊ_CLIENT_ID"
dotnet user-secrets set "PayOS:ApiKey" "GIÁ_TRỊ_API_KEY"
dotnet user-secrets set "PayOS:ChecksumKey" "GIÁ_TRỊ_CHECKSUM_KEY"
```

Khởi động lại API sau khi cấu hình. Không gửi hoặc commit các giá trị này. Trên môi trường Production, đặt `PayOS:ReturnUrl` và `PayOS:CancelUrl` thành địa chỉ HTTPS công khai của Frontend.

MoMo và VNPay cũng cần thông tin merchant riêng. Cấu hình các giá trị từ tài khoản thử nghiệm/merchant tương ứng bằng User Secrets tại cùng thư mục:

```powershell
dotnet user-secrets set "Momo:PartnerCode" "GIÁ_TRỊ_PARTNER_CODE"
dotnet user-secrets set "Momo:AccessKey" "GIÁ_TRỊ_ACCESS_KEY"
dotnet user-secrets set "Momo:SecretKey" "GIÁ_TRỊ_SECRET_KEY"
dotnet user-secrets set "VnPay:TmnCode" "GIÁ_TRỊ_TMN_CODE"
dotnet user-secrets set "VnPay:HashSecret" "GIÁ_TRỊ_HASH_SECRET"
```

User Secrets chỉ được nạp khi Backend chạy ở môi trường Development trên đúng máy đó. Khi triển khai, đặt cùng giá trị bằng biến môi trường trên máy chủ Backend; không lưu secret vào Frontend hoặc chia sẻ qua chat.

## Database và migrations

Các entities đã được tách riêng từng file trong `SportsCenterManagement.DAL/Entities/`:

- **User & membership:** `User`, `Role`, `MemberProfile`, `MembershipPackage`, `MemberSubscription`.
- **Class booking & schedule:** `ClassEntity`, `ClassSchedule`, `ClassSession`, `ClassCoach`, `ClassEnrollment`, `SessionBooking`, `ClassWaitlist`.
- **Payment & report:** `Invoice`, `InvoiceItem`, `Payment`, `PaymentRefund`, `AuditLog`.

Tạo migration mới sau khi cập nhật entity/DbContext:

```powershell
dotnet ef migrations add <MigrationName> --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj --output-dir Migrations
```

Áp dụng thay đổi:

```powershell
dotnet ef database update --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj
```

Cập nhật script SQL khi cần triển khai thủ công:

```powershell
dotnet ef migrations script --idempotent --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj --output .\database\schema.sql
```

## Build

```powershell
dotnet build .\SWP391_SE2039_G06_SportsCenterManagement1.slnx
```
