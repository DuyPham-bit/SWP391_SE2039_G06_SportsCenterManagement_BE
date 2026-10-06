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
| GET | `/api/payments/vnpay-callback` | Xác thực kết quả callback VNPay |
| POST | `/api/invoices/{invoiceId}/payments` | Manager/Receptionist ghi nhận tiền mặt; chỉ kích hoạt khi đã thu đủ |
| GET | `/api/centers/{centerId}/audit-logs` | Manager xem audit của center được phân công |
| GET | `/api/admin/audit-logs` | System Admin xem audit toàn hệ thống |
| GET | `/api/admin/roles` | System Admin xem ma trận quyền |
| PUT | `/api/admin/roles/{roleId}/permissions` | System Admin thay ma trận quyền của role; quyền này luôn chỉ thuộc SystemAdmin |

MVP tắt email verification; mật khẩu cần ít nhất 12 ký tự gồm chữ hoa, chữ thường, số và ký tự đặc biệt. Token bearer dùng ASP.NET Core Data Protection, hết hạn sau 1 giờ; API kiểm tra lại trạng thái và role trên mỗi request. Subscription chỉ bắt đầu khi invoice được thanh toán đủ.

Manager/Receptionist cần có `StaffProfile` trạng thái `Active` tại center được giao. Manager tạo Coach/Receptionist trong center mình quản lý; System Admin có thể quản trị nhân sự theo quyền được cấp. Mọi endpoint nhạy cảm kiểm tra permission và center ở backend.

### Bootstrap System Admin đầu tiên

Role `SystemAdmin` được seed nhưng không có tài khoản/mật khẩu mặc định. Với database chưa có System Admin, cần cấu hình đủ ba biến môi trường trước khi chạy API; nếu thiếu, API dừng khởi động để tránh vận hành hệ thống không có quản trị viên. Mật khẩu phải dài ít nhất 12 ký tự và có chữ hoa, chữ thường, số, ký tự đặc biệt. Khi đã có System Admin, bootstrapper không tự đổi thông tin tài khoản.

```powershell
$env:Bootstrap__SystemAdmin__Username = "<username>"
$env:Bootstrap__SystemAdmin__Email = "<email>"
$env:Bootstrap__SystemAdmin__Password = "<strong-password>"
```

System Admin là role duy nhất được sửa ma trận quyền và xem audit toàn hệ thống. Không cấp `roles.permissions.manage` cho role khác; backend từ chối thao tác này kể cả khi client gọi trực tiếp API.

Yêu cầu tiền mặt gửi `IdempotencyKey` từ 16 đến 100 ký tự cùng số tiền. Retry phải dùng cùng key cho cùng invoice/amount; tái sử dụng key cho yêu cầu khác sẽ trả conflict. Callback VNPay đã ký được lưu cả khi thất bại; callback báo thành công nhưng không khớp trạng thái/số dư được ghi `ReviewRequired`, không tự cộng doanh thu hoặc kích hoạt subscription.

Cổng được in ra khi chạy ứng dụng.

## Cấu hình database

Connection string key là `ConnectionStrings:SportsCenter`. Giá trị mặc định nằm trong `SportsCenterManagement.API/appsettings.json`; `.env.example` chỉ là mẫu vì .NET không tự nạp file `.env`.

PowerShell, chỉ cho phiên terminal hiện tại:

```powershell
$env:ConnectionStrings__SportsCenter = "Server=(localdb)\MSSQLLocalDB;Database=SportsCenterManagement;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

Không commit thông tin đăng nhập hoặc secret thật. Dùng User Secrets hoặc environment variables ngoài máy local.

## Database và migrations

Các entities đã được tách riêng từng file trong `SportsCenterManagement.DAL/Entities/`:

- **User & membership:** `User`, `Role`, `MemberProfile`, `MembershipPackage`, `MemberSubscription`.
- **Class booking & schedule:** `ClassEntity`, `ClassSchedule`, `ClassSession`, `ClassCoach`, `ClassEnrollment`, `SessionBooking`, `ClassWaitlist`.
- **Payment & report:** `Invoice`, `InvoiceItem`, `Payment`, `AuditLog`.

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
