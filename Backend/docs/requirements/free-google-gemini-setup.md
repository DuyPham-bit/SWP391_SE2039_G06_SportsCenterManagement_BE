# Bật Google Login và AI cho bài môn học

Dự án dùng Google Identity Services để đăng nhập và Gemini 3.5 Flash-Lite cho chatbot/gợi ý giáo án. Google yêu cầu cấu hình thuộc tài khoản của bạn; không có Client ID/API key dùng chung trong mã nguồn.

## 1. Tạo Gemini API key

1. Đăng nhập [Google AI Studio](https://aistudio.google.com/apikey), tạo API key cho project của bạn.
2. Dùng Free Tier, không bật billing nếu chỉ thử bài môn học. Hạn mức phụ thuộc tài khoản/project; khi hết hạn mức API trả 429, ứng dụng hiển thị lỗi để thử lại sau.
3. Từ workspace root, lưu key ở backend:

```powershell
dotnet user-secrets set "AI:ApiKey" "DAN_API_KEY_CUA_BAN" --project Backend/Backend/SportsCenterManagement.API
```

Model mặc định là `gemini-3.5-flash-lite`; có thể đổi `AI:Model` qua User Secrets khi model thay đổi. Kiểm tra ngày 11/10/2026: `gemini-2.5-flash-lite` trả 404 cho tài khoản mới; `gemini-3.5-flash-lite` có Free Tier và đã trả lời thành công qua API `generateContent`. Không đặt key vào `VITE_*`, frontend hoặc Git. Free Tier có thể dùng dữ liệu gửi lên để cải thiện sản phẩm; dùng dữ liệu giả cho môn học, không gửi hồ sơ sức khỏe thật.

Nguồn: [bảng giá Gemini](https://ai.google.dev/gemini-api/docs/pricing), [quản lý API key](https://ai.google.dev/gemini-api/docs/api-key). Giá/quota có thể thay đổi.

## 2. Tạo Google OAuth Web Client ID

1. Mở [Google Cloud Console](https://console.cloud.google.com/), chọn/tạo project và cấu hình Google Auth Platform (consent screen). Nếu để Testing, thêm email dùng chấm bài vào danh sách Test users.
2. Tạo OAuth Client với loại **Web application**. Trong Authorized JavaScript origins thêm `http://localhost:3004` (thêm origin của website khi triển khai). Không nhập đường dẫn hoặc `#/login` vào origin.
3. Sao chép Client ID có đuôi `.apps.googleusercontent.com`. Luồng ID token hiện tại không cần client secret hoặc redirect URI.
4. Lưu cùng Client ID ở hai nơi:

```powershell
dotnet user-secrets set "Google:ClientId" "CLIENT_ID.apps.googleusercontent.com" --project Backend/Backend/SportsCenterManagement.API
```

Trong `Frontend/.env.local`:

```dotenv
VITE_API_BASE_URL=/api
VITE_SPORTS_CENTER_ID=ID_TRUNG_TAM_THUC_TE
VITE_GOOGLE_CLIENT_ID=CLIENT_ID.apps.googleusercontent.com
```

Client ID là cấu hình công khai; Gemini API key là bí mật. Hướng dẫn chính thức: [thiết lập Google Identity Services](https://developers.google.com/identity/gsi/web/guides/get-google-api-clientid).

## 3. Khởi động lại và thử

- Giữ `Jwt:Key` riêng dài ít nhất 32 byte trong User Secrets/biến môi trường như hướng dẫn README. Không có JWT secret mặc định.
- Khởi động lại backend; migration `CompleteApiIntegration` được áp dụng theo cơ chế khởi động hiện có. Sao lưu database nếu đang chứa dữ liệu cần giữ.
- Khởi động lại Vite để nạp `.env.local`, mở `http://localhost:3004`.
- Google mới tạo hội viên tại `VITE_SPORTS_CENTER_ID`. Email đã có tài khoản phải đăng nhập bằng mật khẩu rồi chọn **Hồ sơ → Liên kết Google**, xác nhận mật khẩu hiện tại và chọn Google cùng email.
- Hội viên mở **Trợ lý AI**; HLV mở **Gợi ý AI** và chọn học viên thuộc lớp được phân công. Chỉ khi bấm lưu mới ghi giáo án cá nhân.
- Swagger `/swagger` ở Development có nút **Authorize**. Nhập `accessToken` từ `/api/auth/login` hoặc `/api/auth/google`, không thêm lần nữa chữ `Bearer`.

Chưa cấu hình Google/AI thì nghiệp vụ khác vẫn hoạt động; các chức năng này thông báo chưa cấu hình, không giả lập đăng nhập hay kết quả AI. Thanh toán online vẫn cần tài khoản sandbox riêng của VNPay/MoMo/PayOS.
