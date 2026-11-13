# Sports Center Management — Backend

Backend cho hệ thống quản lý trung tâm thể thao. Repository hiện dùng ASP.NET Core Web API theo kiến trúc 3 tầng chuẩn (3-Layer Architecture), EF Core và PostgreSQL.

## Stack và cấu trúc 3 tầng chuẩn (3-Layer Architecture)

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core 10 + PostgreSQL (Npgsql)
- PostgreSQL 17 chạy bằng Docker (`start-postgres.bat`) cho môi trường phát triển mặc định
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

Yêu cầu .NET SDK 10 và PostgreSQL 13+ (khuyến nghị container `postgres` trong `docker/docker-compose.yml`).

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
| GET | `/api/payments/vnpay-callback` | Xác thực kết quả callback VNPay |
| POST | `/api/invoices/{invoiceId}/payments` | Manager/Receptionist ghi nhận tiền mặt; chỉ kích hoạt khi đã thu đủ |
| GET | `/api/centers/{centerId}/audit-logs` | Manager xem audit của center được phân công |
| GET | `/api/admin/audit-logs` | System Admin xem audit toàn hệ thống |
| GET | `/api/admin/roles` | System Admin xem ma trận quyền |
| PUT | `/api/admin/roles/{roleId}/permissions` | System Admin thay ma trận quyền của role; quyền này luôn chỉ thuộc SystemAdmin |

Tự đăng ký yêu cầu OTP xác thực email: gọi `POST /api/auth/request-email-verification` rồi gửi `emailOtp` cùng request đăng ký. Reset mật khẩu cũng gửi OTP qua email và không trả mã trong Development. Mật khẩu cần ít nhất 6 ký tự gồm chữ hoa, chữ thường, số và ký tự đặc biệt. API dùng JWT HS256 qua JwtBearer, kiểm tra chữ ký, issuer, audience và thời hạn (mặc định 60 phút, cấu hình `Jwt:ExpiresMinutes`); trạng thái tài khoản, role và center được đọc lại từ DB mỗi request. Token Data Protection cũ cần đăng nhập lại. Subscription chỉ bắt đầu khi invoice được thanh toán đủ.

Frontend dùng cookie HttpOnly chứa JWT và header `X-SCMS-Session` chọn phiên riêng từng tab; sessionStorage chỉ giữ mã chọn phiên, không chứa JWT. F5 giữ đăng nhập và mỗi tab có thể dùng tài khoản khác nhau. Cookie có SameSite=Strict, Secure trên HTTPS; dùng `/api` cùng origin qua Vite proxy hoặc frontend nhúng. API client ngoài trình duyệt vẫn dùng Bearer JWT. Chi tiết: [cơ chế phiên từng tab](../Frontend/docs/tab-session-fix.md). Nếu dùng frontend nhúng do API phục vụ, chạy `npm.cmd run build:api` tại `Frontend/` trước khi chạy/publish backend để cập nhật `SportsCenterManagement.API/wwwroot` và loại bỏ bundle cũ. `wwwroot` chỉ chứa tài nguyên build; sửa tài nguyên nguồn ở `Frontend/public/`.

Member/Coach gửi và theo dõi yêu cầu qua `GET/POST /api/support-requests/me`, xem chi tiết qua `GET /api/support-requests/me/{id}` và bổ sung phản hồi qua `POST /api/support-requests/me/{id}/messages`. Backend tự lấy người gửi/trung tâm; API tiếp nhận của Lễ tân/Manager giữ phạm vi trung tâm. `PUT /api/notifications/me/read-all` đọc tất cả thông báo của người dùng hiện tại.

Giáo án, tiến độ, thông báo và các endpoint Google/AI được mô tả tại [api-jwt-integration.md](docs/requirements/api-jwt-integration.md). Thiết lập Google Client ID và Gemini Free Tier: [free-google-gemini-setup.md](docs/requirements/free-google-gemini-setup.md).

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

Mã tạo/đặt lại tài khoản demo bằng mật khẩu công khai đã được gỡ, kể cả trong Development. Các cờ `DevelopmentBootstrap` cũ không còn tác dụng. Tạo Admin đầu tiên bằng cấu hình bootstrap riêng của môi trường, sau đó tạo tài khoản qua các luồng quản trị/đăng ký hiện có. Dữ liệu tài khoản cũ không tự bị xóa hoặc sửa; trước deploy cần rà soát và khóa/đổi mật khẩu các tài khoản mẫu còn tồn tại.

`POST /api/mail/send` yêu cầu vai trò `Admin`, kiểm tra người nhận/tiêu đề/nội dung và giới hạn 5 yêu cầu/phút/tài khoản. OTP đăng ký và đặt lại mật khẩu vẫn được gửi qua các endpoint riêng với nội dung do server tạo. Cấu hình secret, bảo vệ Docker build và xử lý credential đã lộ: [hướng dẫn bảo mật](docs/security-config-mail-bootstrap.md).

### Flow 3 — thanh toán và báo cáo

Các API Flow 3 yêu cầu JWT, ngoại trừ callback/IPN do cổng thanh toán gọi. Tài khoản đăng nhập qua `POST /api/auth/login`; role, user, member và center được lấy từ claims, không nhận qua `X-Member-Id` hoặc `X-Staff-Id`. Mọi lệnh ghi thanh toán cần header `Idempotency-Key`.

| Method | Route | Vai trò | Mục đích |
|---|---|---|---|
| POST | `/api/payments/create-vnpay-url` | Member | Tạo hoặc phát lại link VNPay |
| POST | `/api/payments/create-momo-url` | Member | Tạo hoặc phát lại link MoMo |
| GET | `/api/payments/vnpay-callback`, `/api/payments/vnpay-ipn` | Cổng thanh toán | Xác nhận callback/IPN có chữ ký |
| GET/POST | `/api/payments/momo-callback`, `/api/payments/momo-ipn` | Cổng thanh toán | Xác nhận callback/IPN có chữ ký |
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

Schema hiện chưa gắn `MemberProfile` trực tiếp với center. Báo cáo thành viên tính một hội viên thuộc center nếu họ có ít nhất một subscription ở center đó; `NewMembers` được tính theo thời điểm subscription đầu tiên tại center được tạo. Hồ sơ chưa từng mua subscription chưa được phân bổ vào center.

Cổng được in ra khi chạy ứng dụng.

## Cấu hình database

Connection string key là `ConnectionStrings:SportsCenter`. Môi trường Development dùng PostgreSQL tại `localhost:5432` (xem `appsettings.Development.json`). `.env.example` chỉ là mẫu vì .NET không tự nạp file `.env`; đặt các biến cần thiết trong terminal, User Secrets hoặc cấu hình môi trường của nơi triển khai.

PowerShell, chỉ cho phiên terminal hiện tại:

```powershell
$env:ConnectionStrings__SportsCenter = "Host=localhost;Port=5432;Database=SportsCenterDB;Username=postgres;Password=<mat-khau>"
```

Không commit thông tin đăng nhập hoặc secret thật. Dùng User Secrets hoặc environment variables ngoài máy local.

Để dùng database đã có, đặt `ConnectionStrings:SportsCenter` tới đúng instance/database của bạn. Frontend gọi API, không kết nối database trực tiếp. Mặc định ứng dụng chỉ đồng bộ schema/quyền/danh mục môn, không tự thêm tài khoản, lớp, gói tập hoặc giao dịch demo; đặt `Database__SeedDemoData=true` chỉ trên database demo nếu muốn tạo dữ liệu mẫu (seed này gán gói, hóa đơn đã thanh toán và payment mẫu cho mọi hội viên hiện có). Cấu hình local đã kiểm tra cho workspace này và hướng dẫn chạy: [Kết nối DB thật](../Frontend/docs/real-database.md).

JWT cần secret riêng dài tối thiểu 32 byte. Cấu hình bắt buộc `Jwt__Key` trước khi chạy. Có thể tạo nhanh cho phiên PowerShell hiện tại:

```powershell
$jwtBytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Fill($jwtBytes)
$env:Jwt__Key = [Convert]::ToBase64String($jwtBytes)
$env:Jwt__Issuer = "SportsCenterManagement"
$env:Jwt__Audience = "SportsCenterManagement.FE"
$env:Cors__AllowedOrigins__0 = "http://localhost:3004"
```

Gateway secret được đọc từ biến môi trường/User Secrets: `VnPay__HashSecret`, `Momo__AccessKey`, `Momo__SecretKey`. Không cấu hình provider thì các API khác vẫn chạy; endpoint tạo link của provider đó trả `503` trước khi tạo payment attempt.

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

Phân quyền R2, thư viện media và migration liên quan: [media-access-scope.md](docs/media-access-scope.md).

```powershell
dotnet build .\SWP391_SE2039_G06_SportsCenterManagement1.slnx
```
