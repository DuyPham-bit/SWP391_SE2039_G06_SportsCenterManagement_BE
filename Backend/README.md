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

```powershell
dotnet restore .\SWP391_SE2039_G06_SportsCenterManagement1.slnx
dotnet tool restore
dotnet ef database update --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj
dotnet run --project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj
```

API được xây bằng ASP.NET Core MVC Controllers và trả JSON; không dùng Razor Views vì repository này là backend cho frontend riêng. Các route GET hiện có:

| Method | Route | Mục đích |
|---|---|---|
| GET | `/health` hoặc `/api/health` | Health check, trả `{ "status": "ok" }` |
| GET | `/api/centers/{centerId}/membership-packages` | Danh sách gói Active của center |
| GET | `/api/centers/{centerId}/classes` | Danh sách lớp Published của center |

Khi chạy với environment `Development`, backend tạo center/gói mẫu và các tài khoản demo `admin`, `manager01`, `reception01`, `member01` để phát triển local. Những dữ liệu mẫu này không được tự tạo ở môi trường khác; môi trường triển khai cần được cấp dữ liệu trung tâm, gói tập và tài khoản qua quy trình quản trị riêng. Không dùng mật khẩu demo cho môi trường triển khai. Nếu database đã từng được khởi tạo ở `Development`, hãy xóa hoặc đổi mật khẩu tài khoản demo và rà lại center/gói mẫu trước khi tái sử dụng ở môi trường khác.

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

Connection string key là `ConnectionStrings:SportsCenter`. Giá trị mặc định nằm trong `SportsCenterManagement.API/appsettings.json`; `.env.example` chỉ là mẫu vì .NET không tự nạp file `.env`.

PowerShell, chỉ cho phiên terminal hiện tại:

```powershell
$env:ConnectionStrings__SportsCenter = "Server=(localdb)\MSSQLLocalDB;Database=SportsCenterManagement;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

Không commit thông tin đăng nhập hoặc secret thật. Dùng User Secrets hoặc environment variables ngoài máy local.

JWT cần secret riêng dài tối thiểu 32 byte. Có thể tạo nhanh cho phiên PowerShell hiện tại:

```powershell
$jwtBytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Fill($jwtBytes)
$env:Jwt__SecretKey = [Convert]::ToBase64String($jwtBytes)
$env:Jwt__Issuer = "SportsCenterManagement"
$env:Jwt__Audience = "SportsCenterManagement.Client"
$env:Cors__AllowedOrigins__0 = "http://localhost:5173"
```

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
