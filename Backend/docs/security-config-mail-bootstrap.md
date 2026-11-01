# Cấu hình secret, API email và tài khoản khởi tạo

Các file `appsettings*.json` đang được Git theo dõi đều để trống mật khẩu, JWT key, R2 access/secret key và connection string. Cấu hình thực tế lấy từ biến môi trường, `.NET user-secrets` hoặc secret được mount khi triển khai. Không điền secret vào các file này hoặc các file `.env.example`.

`.gitignore` bảo vệ `.env`, `.env.*` và `credentials.local.json`. Docker build cũng loại các file cấu hình riêng này. Ngoại lệ `Frontend/.env.local` đang được theo dõi chỉ chứa bốn khóa công khai phục vụ build: `VITE_API_BASE_URL`, `SCMS_API_PROXY_TARGET`, `VITE_SPORTS_CENTER_ID`, `VITE_GOOGLE_CLIENT_ID`; giữ ngoại lệ để Docker tiếp tục có cấu hình đăng ký/Google hiện tại. Bộ kiểm tra từ chối khóa khác trong file này và các trường secret không rỗng trong mọi bản sao appsettings được theo dõi.

`docker/.env` tiếp tục được dùng làm cấu hình runtime bởi Docker Compose, không được COPY vào image. Với .NET local, dùng `dotnet user-secrets` tại project API hoặc biến môi trường; ví dụ cấu hình `Jwt__Key`, `R2Storage__AccessKey`, `R2Storage__SecretKey`, `Bootstrap__Admin__Password`. Bộ khởi tạo Admin hiện có vẫn hỗ trợ tên cấu hình `Bootstrap__SystemAdmin__*` cũ.

## API gửi thư

`POST /api/mail/send` chỉ cho phép tài khoản `Admin` đã xác thực bằng cơ chế JWT hiện có. Người chưa đăng nhập nhận 401; Member, Coach, Receptionist hoặc Manager nhận 403. API yêu cầu email người nhận hợp lệ, tiêu đề tối đa 200 ký tự và nội dung tối đa 20.000 ký tự; không tự chọn người nhận mặc định. Mỗi Admin được gửi tối đa 5 yêu cầu trong một phút; vượt giới hạn nhận 429.

OTP đăng ký, đổi mật khẩu và đặt lại mật khẩu dùng dịch vụ riêng, nội dung do backend tạo và các giới hạn hiện có. Việc khóa API gửi thư tùy ý không thay đổi những luồng này.

## Tài khoản development

Không còn mã tạo hoặc đặt lại tài khoản mẫu bằng mật khẩu đã công khai. Các cờ `DevelopmentBootstrap:SeedDemoAccounts`, `ResetExistingDemoAccounts`, `CenterId` cũ không còn được đọc. Development và Production đều dùng bộ khởi tạo Admin yêu cầu thông tin riêng của người vận hành; các tài khoản khác được tạo qua luồng nghiệp vụ hiện có.

Việc bỏ seeder không sửa dữ liệu tài khoản đã tồn tại. Trước deploy, dùng giao diện quản trị để rà soát các tài khoản mẫu được tạo trước đây; khóa tài khoản không dùng hoặc đặt lại mật khẩu bằng giá trị riêng. Không xóa tài khoản theo tên nếu đã gắn với giao dịch, lớp hoặc hồ sơ thật.

## Credential đã xuất hiện trong Git

Rà soát chỉ đọc xác nhận lịch sử của `Backend/Backend/SportsCenterManagement.API/appsettings.Development.json` từng chứa trường private không rỗng, gồm JWT key, mật khẩu bootstrap và credential của dịch vụ. Việc để trống ở phiên bản hiện tại không thu hồi credential trong commit cũ.

Người vận hành cần thực hiện trước deploy:

1. Thu hồi/thay R2 key, SMTP password, API key, refresh token hoặc credential khác đã lộ tại dịch vụ tương ứng; cập nhật cấu hình runtime riêng.
2. Tạo JWT signing key mới đủ mạnh và cập nhật runtime. Việc này làm token cũ mất hiệu lực và người dùng cần đăng nhập lại.
3. Đổi mật khẩu Admin và các tài khoản mẫu đã được tạo bằng mật khẩu công khai. Xóa giá trị bootstrap sau lần khởi tạo đầu tiên nếu môi trường triển khai cho phép.
4. Sau khi thu hồi secret, phối hợp với người quản lý repository để làm sạch lịch sử/bản sao Git nếu cần. Viết lại lịch sử và force-push ảnh hưởng cộng tác viên, nên không được thực hiện tự động trong bản sửa này.

Bản sửa source không tự thay secret ở nhà cung cấp, không đổi mật khẩu trong database hiện hữu và không viết lại lịch sử Git.

## Kiểm chứng

- `Frontend`: `node --test tests/config-secrets.test.cjs` và `npm test`.
- Từ gốc repository: `dotnet run --project Backend/verification/LogicAudit/LogicAudit.csproj` chạy HTTP/SQL Server trên database kiểm thử riêng, dùng mail fixture không gửi thư ra ngoài.
