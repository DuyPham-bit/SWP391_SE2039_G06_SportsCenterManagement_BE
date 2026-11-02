# Database Setup & Scripts

Cơ sở dữ liệu SCMS sử dụng **SQL Server 2019/2022** và Entity Framework Core.

## 🚀 Khởi tạo nhanh bằng 1 file SQL duy nhất (Khuyên dùng khi bàn giao/chấm bài)

File [`init_database.sql`](./init_database.sql) đã được gộp toàn bộ cấu trúc bảng (Schema DDL) và dữ liệu mẫu đầy đủ (Seed Data: Admin, HLV, Hội viên, Lớp học, Lịch tập, KPI):

1. Mở file `Backend/database/init_database.sql` trong **SQL Server Management Studio (SSMS)** hoặc **Azure Data Studio**.
2. Kết nối tới SQL Server của bạn.
3. Nhấn **Execute (F5)**. File sẽ tự động tạo cơ sở dữ liệu `SportsCenterManagement`, tạo toàn bộ bảng và nạp dữ liệu mẫu hoàn chỉnh.

### Tài khoản mẫu sau khi chạy:
- **System Admin**: `admin` / `Admin@SCMS2026!Secure` (hoặc `Admin@123456`)
- **Manager**: `manager01` / `Member@123456`
- **Coach / Trainer**: `coach01` / `Member@123456`
- **Hội viên**: `FLOW2-MEM-001` / `Member@123456`

---

## 🛠 Quản lý Migrations bằng EF Core (Dành cho Lập trình viên)

Từ thư mục `Backend`:

```powershell
# Cập nhật database theo Migration mới nhất
dotnet ef database update --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj

# Thêm Migration mới khi sửa Model
dotnet ef migrations add <TenMigration> --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj --output-dir Migrations
```
