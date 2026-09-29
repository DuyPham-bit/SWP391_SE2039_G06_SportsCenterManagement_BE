# Sports Center Management — Backend

Backend cho hệ thống quản lý trung tâm thể thao. Repository hiện dùng ASP.NET Core Web API, EF Core và SQL Server LocalDB.

## Stack và cấu trúc

- .NET 10 / ASP.NET Core
- Entity Framework Core 10 + SQL Server
- SQL Server LocalDB cho môi trường phát triển mặc định

```text
.
├── SportsCenterManagement.API/
│   ├── Controllers/                  # MVC controllers trả JSON cho frontend
│   ├── Program.cs                    # DI, middleware và route mapping
│   └── appsettings.json
├── SportsCenterManagement.Services/
│   ├── Features/CoreFlows/           # Use cases cho membership, enrollment, payment/report
│   ├── Migrations/                   # EF Core migrations
│   └── SportsCenterDbContext.cs
├── SportsCenterManagement.Models/    # Entities dùng chung
├── database/                         # SQL schema sinh từ migrations
├── docs/requirements/core-flows.md   # Use cases, happy/unhappy cases, AC và DB đề xuất
└── SWP391_SE2039_G06_SportsCenterManagement1.slnx
```

## Chạy local

Yêu cầu .NET SDK 10 và SQL Server LocalDB hoặc SQL Server tương thích.

```powershell
dotnet restore .\SWP391_SE2039_G06_SportsCenterManagement1.slnx
dotnet tool restore
dotnet ef database update --project .\SportsCenterManagement.Services\SportsCenterManagement.Services.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj
dotnet run --project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj
```

API được xây bằng ASP.NET Core MVC Controllers và trả JSON; không dùng Razor Views vì repository này là backend cho frontend riêng. Các route GET hiện có:

| Method | Route | Mục đích |
|---|---|---|
| GET | `/health` hoặc `/api/health` | Health check, trả `{ "status": "ok" }` |
| GET | `/api/centers/{centerId}/membership-packages` | Danh sách gói Active của center |
| GET | `/api/centers/{centerId}/classes` | Danh sách lớp Published của center |

Cổng được in ra khi chạy ứng dụng.

## Cấu hình database

Connection string key là `ConnectionStrings:SportsCenter`. Giá trị mặc định nằm trong `SportsCenterManagement.API/appsettings.json`; `.env.example` chỉ là mẫu vì .NET không tự nạp file `.env`.

PowerShell, chỉ cho phiên terminal hiện tại:

```powershell
$env:ConnectionStrings__SportsCenter = "Server=(localdb)\MSSQLLocalDB;Database=SportsCenterManagement;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

Không commit thông tin đăng nhập hoặc secret thật. Dùng User Secrets hoặc environment variables ngoài máy local.

## Database và migrations

Các entities đã có nền tảng cho ba flow bắt buộc:

- **User & membership:** `User`, `Role`, `MemberProfile`, `MembershipPackage`, `MemberSubscription`.
- **Class booking & schedule:** `ClassEntity`, `ClassSchedule`, `ClassSession`, `ClassCoach`, `ClassEnrollment`, `SessionBooking`, `ClassWaitlist`.
- **Payment & report:** `Invoice`, `InvoiceItem`, `Payment`, `AuditLog`.

Migration `CoreFlowUniqueness` thêm các ràng buộc chống đăng ký lặp `(SessionId, MemberId)`, enrollment lặp `(ClassId, MemberId)` và transaction code bị lặp (chỉ khi có mã). Vì mỗi cặp chỉ có một bản ghi, khi hủy rồi đăng ký lại cần tái sử dụng bản ghi và cập nhật trạng thái thay vì insert mới.

`SportsCenterManagement.Services/Features/CoreFlows/CoreFlowService.cs` hiện có application use cases cho: liệt kê gói Active; tạo subscription chờ thanh toán cùng invoice/item; ghi danh lớp với kiểm tra membership, center, quota và capacity; ghi nhận thanh toán tiền mặt, cập nhật invoice/kích hoạt subscription; tổng hợp doanh thu gộp theo center và kỳ.

Tạo migration mới sau khi cập nhật entity/DbContext:

```powershell
dotnet ef migrations add <MigrationName> --project .\SportsCenterManagement.Services\SportsCenterManagement.Services.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj --output-dir Migrations
```

Áp dụng thay đổi:

```powershell
dotnet ef database update --project .\SportsCenterManagement.Services\SportsCenterManagement.Services.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj
```

Cập nhật script SQL khi cần triển khai thủ công:

```powershell
dotnet ef migrations script --idempotent --project .\SportsCenterManagement.Services\SportsCenterManagement.Services.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj --output .\database\schema.sql
```

## Trạng thái triển khai ba flow

| Flow | Đã có trong model/database | Còn cần triển khai |
|---|---|---|
| User & membership | User/role/member profile, package, subscription; tạo subscription chờ thanh toán trong service | API đăng ký/tra cứu/gia hạn, validation hồ sơ và xác thực tài khoản |
| Class booking & schedule | Class, schedule/session, coach assignment, enrollment/booking/waitlist; service ghi danh lớp có kiểm tra capacity | API quản lý lịch, hủy, waitlist, xung đột coach/phòng và phân quyền |
| Payment & report | Invoice/items/payment; ghi tiền mặt và báo cáo gross trong service | API bảo mật, cổng thanh toán/callback, refund, xuất hóa đơn và báo cáo gross/refund/net |

Các quy tắc nghiệp vụ, happy/unhappy cases, acceptance criteria và Open Questions nằm trong [`docs/requirements/core-flows.md`](docs/requirements/core-flows.md). Tài liệu đang ở trạng thái Draft. API chưa cấu hình authentication/authorization; các route GET hiện tại chỉ cung cấp dữ liệu danh mục. `CoreFlowService` đã cài use cases nhưng các thao tác ghi chưa được mở từ controller. Cần hoàn thành xác thực/phân quyền và chốt các chính sách còn mở trước khi công khai chúng cho client. Payment hiện chỉ hỗ trợ ghi nhận tiền mặt; refund chưa được lưu thành giao dịch riêng, nên báo cáo trả refund bằng 0.

## Build

```powershell
dotnet build .\SWP391_SE2039_G06_SportsCenterManagement1.slnx
```

Chưa có test project hoặc cấu hình lint riêng trong solution hiện tại. Cập nhật mục này khi nhóm thêm các quality checks đó.
