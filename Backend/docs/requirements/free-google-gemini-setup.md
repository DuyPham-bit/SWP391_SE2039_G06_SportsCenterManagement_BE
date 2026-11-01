# Bật Google Login và AI cho bài môn học

Dự án dùng Google Identity Services để đăng nhập và Gemini 3.5 Flash-Lite cho chatbot/gợi ý giáo án. Google yêu cầu cấu hình thuộc tài khoản của bạn; không có Client ID/API key dùng chung trong mã nguồn.

## 1. Tạo Gemini API key

1. Đăng nhập [Google AI Studio](https://aistudio.google.com/apikey), tạo API key cho project của bạn.
2. Dùng Free Tier, không bật billing nếu chỉ thử bài môn học. Hạn mức phụ thuộc tài khoản/project; khi hết hạn mức API trả 429, ứng dụng hiển thị lỗi để thử lại sau.
3. Từ workspace root, lưu key ở backend:

```powershell
dotnet user-secrets set "AI:ApiKey" "DAN_API_KEY_CUA_BAN" --project Backend/SportsCenterManagement.API
```

Model đang được backend sử dụng là `gemini-3.5-flash-lite`. Nếu cần đổi model, cập nhật hằng `GeminiService.ModelName` và kiểm thử lại API; cấu hình `AI:Model` hiện chưa được mã nguồn đọc. Kiểm tra ngày 23/10/2026: `gemini-3.5-flash-lite` đã trả lời thành công qua API `generateContent`. Không đặt key vào `VITE_*`, frontend hoặc Git. Free Tier có thể dùng dữ liệu gửi lên để cải thiện sản phẩm; dùng dữ liệu giả cho môn học, không gửi hồ sơ sức khỏe thật.

### Cấu hình lại sau khi clone trên máy khác

User Secrets không được lưu trong repository và không tự đi theo lệnh `git clone`. Mỗi máy phát triển cần có .NET SDK 10 và tự lưu API key của mình từ thư mục gốc repository:

```powershell
dotnet user-secrets set "AI:ApiKey" "DAN_API_KEY_CUA_BAN" --project Backend/SportsCenterManagement.API/SportsCenterManagement.API.csproj
dotnet user-secrets list --project Backend/SportsCenterManagement.API/SportsCenterManagement.API.csproj
dotnet run --project Backend/SportsCenterManagement.API/SportsCenterManagement.API.csproj
```

Lệnh `list` chỉ dùng để kiểm tra trên máy cá nhân; không chụp màn hình, gửi log hoặc dán kết quả có key vào issue/chat. Sau khi backend khởi động, kiểm tra `http://localhost:54162/api/health` hoặc cổng được in trong terminal. Frontend chạy riêng bằng `npm.cmd run dev -- --host 127.0.0.1` trong thư mục `Frontend`.

### Chạy bằng Docker Compose

Sao chép `docker/.env.example` thành `docker/.env`, sau đó điền key thật vào file local:

```dotenv
AI_API_KEY=DAN_API_KEY_CUA_BAN
```

`docker/.env` khớp quy tắc `*.env` trong `.gitignore`, còn `docker/.env.example` chỉ chứa tên biến và được phép commit. Compose chuyển biến này vào ASP.NET Core dưới tên `AI__ApiKey`; dấu `__` được .NET ánh xạ thành `AI:ApiKey`.

```powershell
docker compose -f docker/docker-compose.yml --env-file docker/.env up -d --build
```

### GitHub Actions hoặc máy chủ triển khai

Tạo GitHub Actions repository secret tên `AI_API_KEY` tại **Settings → Secrets and variables → Actions**. Trong workflow, truyền secret vào tiến trình backend bằng biến môi trường:

```yaml
env:
  AI__ApiKey: ${{ secrets.AI_API_KEY }}
```

Nếu triển khai trực tiếp lên Render, Railway, Azure, VPS hoặc dịch vụ tương tự, đặt biến môi trường `AI__ApiKey` trong phần cấu hình của dịch vụ. Không tạo key trong file `appsettings.json`, không ghi key thật vào `.env.example`, và không đưa key vào image Docker hoặc biến `VITE_*` vì các biến frontend sẽ bị công khai trong bundle trình duyệt.

### Credential Google Drive cho thư viện media

`GoogleDrive:ClientSecret` và `GoogleDrive:RefreshToken` cũng không được lưu trong Git. Máy cần dùng upload Google Drive phải cấu hình riêng:

```powershell
dotnet user-secrets set "GoogleDrive:ClientSecret" "CLIENT_SECRET_CUA_BAN" --project Backend/SportsCenterManagement.API/SportsCenterManagement.API.csproj
dotnet user-secrets set "GoogleDrive:RefreshToken" "REFRESH_TOKEN_CUA_BAN" --project Backend/SportsCenterManagement.API/SportsCenterManagement.API.csproj
```

Với Docker, đặt hai giá trị tương ứng trong `docker/.env`:

```dotenv
GOOGLE_DRIVE_CLIENT_SECRET=CLIENT_SECRET_CUA_BAN
GOOGLE_DRIVE_REFRESH_TOKEN=REFRESH_TOKEN_CUA_BAN
```

Trên máy chủ có thể đặt trực tiếp `GoogleDrive__ClientSecret` và `GoogleDrive__RefreshToken`. Nếu credential từng xuất hiện trong commit hoặc nơi công khai, phải thu hồi/tạo lại tại Google Cloud trước khi sử dụng.

Nguồn: [bảng giá Gemini](https://ai.google.dev/gemini-api/docs/pricing), [quản lý API key](https://ai.google.dev/gemini-api/docs/api-key). Giá/quota có thể thay đổi.

## 2. Tạo Google OAuth Web Client ID

1. Mở [Google Cloud Console](https://console.cloud.google.com/), chọn/tạo project và cấu hình Google Auth Platform (consent screen). Nếu để Testing, thêm email dùng chấm bài vào danh sách Test users.
2. Tạo OAuth Client với loại **Web application**. Trong Authorized JavaScript origins thêm `http://localhost:3004` (thêm origin của website khi triển khai). Không nhập đường dẫn hoặc `#/login` vào origin.
3. Sao chép Client ID có đuôi `.apps.googleusercontent.com`. Luồng ID token hiện tại không cần client secret hoặc redirect URI.
4. Lưu cùng Client ID ở hai nơi:

```powershell
$googleClientId = (Read-Host 'Dán OAuth Web Client ID thực tế từ Google Cloud').Trim()
if ($googleClientId -notmatch '^\d+-[a-zA-Z0-9_-]+\.apps\.googleusercontent\.com$') {
    throw 'Client ID chưa hợp lệ. Không nhập giá trị mẫu CLIENT_ID.apps.googleusercontent.com.'
}
dotnet user-secrets set "Google:ClientId" $googleClientId --project Backend/SportsCenterManagement.API
```

Trong `Frontend/.env.local`:

```dotenv
VITE_API_BASE_URL=/api
VITE_SPORTS_CENTER_ID=ID_TRUNG_TAM_THUC_TE
VITE_GOOGLE_CLIENT_ID=CLIENT_ID.apps.googleusercontent.com
```

Client ID là cấu hình công khai; Gemini API key là bí mật. Hướng dẫn chính thức: [thiết lập Google Identity Services](https://developers.google.com/identity/gsi/web/guides/get-google-api-clientid).

### Lỗi “ID token Google không hợp lệ hoặc đã hết hạn”

Trong Development, `Google:ClientId` trong User Secrets được ưu tiên hơn `appsettings.json`. Vì vậy, appsettings và frontend giống nhau vẫn có thể lỗi nếu User Secrets còn Client ID mẫu hoặc ID của ứng dụng khác. Token Google phải có `aud` khớp Client ID mà backend thực sự sử dụng.

Đã phát hiện và sửa trường hợp này trên môi trường local ngày 12/10/2026: `Frontend/.env.local` và appsettings dùng cùng Client ID thật, nhưng User Secrets ghi đè bằng giá trị mẫu. Đã cập nhật riêng `Google:ClientId` trong User Secrets và xác minh mọi khóa khác giữ nguyên. Không đổi code xác thực hoặc nới điều kiện chữ ký/issuer/audience/thời hạn token.

Kiểm chứng sau sửa: ba nguồn cấu hình dùng cùng Client ID đúng định dạng; endpoint Google metadata/khóa RS256 truy cập được, API local `/api/health` trả 200. Kiểm tra thời gian qua phản hồi Google không cache lệch khoảng một giây. Chưa xác nhận đăng nhập Google thật vì cần người dùng chọn tài khoản và cấp credential mới trên website.

Khi gặp lại, lấy Client ID thực tế trong `VITE_GOOGLE_CLIENT_ID`, đặt cùng giá trị cho `Google:ClientId` bằng lệnh trên, khởi động lại backend rồi tải lại trang và chọn tài khoản Google để nhận credential mới. Nếu frontend đã đổi `.env.local`, khởi động lại Vite hoặc build lại giao diện nhúng. Không gửi Client Secret, JWT của ứng dụng hoặc access token vào trường `idToken`.

Nguồn: [thứ tự ưu tiên cấu hình ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0#security-and-user-secrets), [xác minh Google ID token](https://developers.google.com/identity/gsi/web/guides/verify-google-id-token).

## 3. Khởi động lại và thử

- Giữ `Jwt:Key` riêng dài ít nhất 32 byte trong User Secrets/biến môi trường như hướng dẫn README. Không có JWT secret mặc định.
- Khởi động lại backend; migration `CompleteApiIntegration` được áp dụng theo cơ chế khởi động hiện có. Sao lưu database nếu đang chứa dữ liệu cần giữ.
- Khởi động lại Vite để nạp `.env.local`, mở `http://localhost:3004`.
- Google mới tạo hội viên tại `VITE_SPORTS_CENTER_ID`. Email đã có tài khoản phải đăng nhập bằng mật khẩu rồi chọn **Hồ sơ → Liên kết Google**, xác nhận mật khẩu hiện tại và chọn Google cùng email.
- Hội viên mở **Trợ lý AI**; HLV mở **Gợi ý AI** và chọn học viên thuộc lớp được phân công. Chỉ khi bấm lưu mới ghi giáo án cá nhân.
- Swagger `/swagger` ở Development có nút **Authorize**. Nhập `accessToken` từ `/api/auth/login` hoặc `/api/auth/google`, không thêm lần nữa chữ `Bearer`.

Chưa cấu hình Google/AI thì nghiệp vụ khác vẫn hoạt động; các chức năng này thông báo chưa cấu hình, không giả lập đăng nhập hay kết quả AI. Thanh toán online vẫn cần tài khoản sandbox riêng của VNPay/MoMo/PayOS.
