# Database Setup - SportsCenterDB

Cơ sở dữ liệu SCMS sử dụng **PostgreSQL 17** (Npgsql) và Entity Framework Core.

## Khởi tạo nhanh bằng 1 file SQL duy nhất

File [`SportsCenterDB.sql`](./SportsCenterDB.sql) chứa **toàn bộ**:
- Schema DDL (tạo bảng, index, constraints)
- EF Core Migrations
- Seed Data (Admin, HLV, Hội viên, Lớp học, Lịch tập)
- Demo Data (subscriptions, invoices, payments, check-ins, attendance)
- Revenue Distribution (phân bố doanh thu thực tế)

### Cách chạy:

File được tạo bằng `pg_dump`, chạy bằng `psql` trên một database **trống**:

```powershell
docker compose -f docker/docker-compose.yml --env-file docker/.env up -d postgres
docker exec -i scms-postgres psql -U postgres -d SportsCenterDB -v ON_ERROR_STOP=1 < Backend/database/SportsCenterDB.sql
```

Không cần chạy file này nếu chỉ muốn schema: backend tự tạo database và áp EF Core migrations khi khởi động.

### Chuyển dữ liệu từ SQL Server cũ

Tool [`tools/SqlServerToPostgres`](../tools/SqlServerToPostgres/Program.cs) chép toàn bộ bảng `dbo` từ database SQL Server cũ sang PostgreSQL đã áp migrations (TRUNCATE rồi nạp lại, đặt lại sequence id):

```powershell
dotnet ef database update --project .\SportsCenterManagement.DAL --startup-project .\SportsCenterManagement.API
$env:SCMS_SOURCE_SQLSERVER = "Server=127.0.0.1,14333;Database=SportsCenterDB;User Id=sa;Password=...;TrustServerCertificate=True"
$env:SCMS_TARGET_POSTGRES  = "Host=localhost;Port=5432;Database=SportsCenterDB;Username=postgres;Password=..."
dotnet run --project .	ools\SqlServerToPostgres
```

### Tài khoản mẫu sau khi chạy:
- **System Admin**: `admin` / `Admin@SCMS2026!Secure` (hoặc `Admin@123456`)
- **Manager**: `manager01` / `Member@123456`
- **Coach / Trainer**: `coach01` / `Member@123456`
- **Hội viên**: `FLOW2-MEM-001` / `Member@123456`

---

## Quản lý Migrations bằng EF Core (Dành cho Lập trình viên)

Từ thư mục `Backend`:

```powershell
# Cập nhật database theo Migration mới nhất
dotnet ef database update --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj

# Thêm Migration mới khi sửa Model
dotnet ef migrations add <TenMigration> --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj --output-dir Migrations
```
