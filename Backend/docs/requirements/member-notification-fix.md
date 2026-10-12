# Sửa thông báo đến hội viên — 12/10/2026

Nguồn yêu cầu: người dùng báo member không thấy thông báo và xác nhận lỗi xảy ra từ nhiều nguồn.

## Nguyên nhân và thay đổi

- Backend chỉ phát thông báo ở luồng HLV gửi trực tiếp và đổi/hủy lịch. Các nghiệp vụ bên dưới chưa tạo `Notification` và `UserNotification` cho tài khoản hội viên.
- Dashboard lấy thông báo một lần lúc mở trang. Dashboard nay dùng `useNotifications` như chuông và hộp thư: tự tải mỗi 15 giây khi trang hiển thị, tải lại khi cửa sổ nhận focus, đồng bộ trạng thái đọc qua sự kiện hiện có.
- API hỗ trợ trả HTTP 500 vì validation của positional record được gắn vào property, và truy vấn lọc record `SupportSummary` không dịch được thành SQL. Chuyển validation sang constructor parameter; lọc, phân trang và sắp xếp entity trước khi tạo record. Giữ các ràng buộc dữ liệu và phạm vi trung tâm.

| Nguồn | Thông báo bổ sung | Người nhận |
|---|---|---|
| Đặt/hủy buổi tập; ghi danh/hủy ghi danh lớp | `BookingConfirmed`, `BookingCancelled` | Hội viên thực hiện hoặc được nhân viên thao tác hộ |
| Tham gia danh sách chờ; được chuyển thành có chỗ | `WaitlistJoined`, `WaitlistPromoted` | Hội viên trong danh sách chờ |
| Tạo/sửa/thu hồi giáo án | `TrainingPlanUpdated` | Học viên lớp; giáo án riêng chỉ gửi người được chỉ định |
| Ghi kết quả tập luyện | `ProgressUpdated` | Hội viên có kết quả mới |
| Thanh toán đủ và kích hoạt gói | `MembershipActivated` | Chủ subscription, qua helper dùng chung của thanh toán tại quầy/cổng online và API tiền mặt cũ |
| Tiếp nhận yêu cầu; đổi trạng thái hoặc trả lời hỗ trợ | `SupportUpdated` | Chủ yêu cầu hỗ trợ |

Thông báo và quan hệ người nhận lưu trong transaction nghiệp vụ hiện có. Helper ánh xạ `MemberProfile.Id` sang `User.Id`; hộp thư vẫn lọc bằng JWT của người nhận. Retry đặt chỗ, vào danh sách chờ và thanh toán không tạo thêm thông báo. Cập nhật hỗ trợ không đổi trạng thái và không có nội dung phản hồi không phát thông báo.

## Quyết định tương thích

- Giữ React/Vite, ASP.NET Core/.NET 10, EF Core và SQL Server. Không thay schema, migration, connection string hoặc cấu hình `.env.local`.
- Giữ contract `GET /api/notifications/me` và `PUT /api/notifications/{id}/read`, quyền sở hữu, giới hạn 200 thông báo mới nhất và các quyền nghiệp vụ hiện có.
- Thông báo mới phát khi nghiệp vụ tương ứng xảy ra sau bản sửa. Không tạo bù thông báo cho lịch sử cũ. Nhắc hạn gói/nhắc giờ học theo thời gian vẫn dùng `MemberReminders` hiện có.

## Kiểm chứng

Đã tái hiện trước bản sửa bằng assertion HTTP/SQL: tạo giáo án thành công nhưng hộp thư thiếu `TrainingPlanUpdated`. Kiểm tra HLV gửi trực tiếp nay kiểm tra thực sự ID thông báo và trạng thái chưa đọc, thay vì chỉ kiểm tra HTTP 200.

Lệnh từ workspace root:

```powershell
dotnet build Backend/Backend/tests/ApiIntegration.Regression/ApiIntegration.Regression.csproj --no-restore
dotnet run --no-restore --project Backend/Backend/tests/Flow2.Regression/Flow2.Regression.csproj
dotnet run --no-build --no-restore --project Backend/Backend/tests/ApiIntegration.Regression/ApiIntegration.Regression.csproj -- --sql --browser
```

Lệnh từ `Frontend/`: `npm.cmd test`, `npm.cmd run build`, `node --check tests/browser-smoke.cjs`.

Browser smoke dùng Chrome headless/CDP của project, database LocalDB riêng có tên `SCMS_ApiIntegration_Test_<guid>`. Nếu `.env.local` có Google Client ID, tạo bản build kiểm thử với `VITE_GOOGLE_CLIENT_ID` rỗng và `VITE_API_BASE_URL=/api` trong môi trường tiến trình Node, rồi đặt `SCMS_TEST_WEB_ROOT` trỏ đến bản build đó. Không sửa cấu hình local để phục vụ kiểm thử. Log API nằm ở `.validation/api-integration-runtime.log`; báo cáo và ảnh browser ở `.validation/browser/`.

Kết quả:

| Kiểm tra | Kết quả |
|---|---|
| Build .NET | Đạt; 0 warning, 0 error |
| Flow2 regression | 10 kiểm tra input và 4 kiểm tra phân quyền đạt |
| API/SQL bản cuối | 180 kiểm tra đạt; database riêng `SCMS_ApiIntegration_Test_2ed50e6974fe426f8df6b9d8e6387e7c` |
| Frontend regression | 43/43 đạt |
| Vite build | Đạt, gồm bản build kiểm thử riêng |
| Chrome/CDP | 41/41 đạt, 0 JavaScript exception; có ảnh desktop/mobile |
| Cú pháp browser runner và `git diff --check` hai repository | Đạt |

API/SQL đã kiểm tra gửi đúng tài khoản, không gửi cho hội viên khác trung tâm hoặc bạn cùng lớp khi giáo án riêng được cập nhật, retry đặt chỗ/danh sách chờ/thanh toán, thanh toán một phần chưa kích hoạt gói, API tiền mặt cũ, phản hồi hỗ trợ, lọc/phân trang hỗ trợ, validation 400 và read state sau reload. Chrome kiểm tra có thông báo mới trong khi dashboard vẫn mở qua focus và polling, badge chuông, đồng bộ trạng thái đọc với dashboard, bộ lọc chưa đọc và hộp thư trên mobile.

Các file BE sửa: `FlowNotificationWriter.cs`, `SessionBookingService.cs`, `CoreFlowService.cs`, `PaymentService.cs`, `TrainingService.cs`, `SupportRequestService.cs`, `SupportDtos.cs` và `tests/ApiIntegration.Regression/Program.cs`. Các file FE sửa: `useNotifications.js`, `MemberDashboard.jsx`, `MemberNotifications.jsx` và `tests/browser-smoke.cjs`.

## Bàn giao

- BE: `SWP391_SE2039_G06_SportsCenterManagement_BE`, nhánh `feature/be-fixes-and-support`, remote `https://github.com/DuyPham-bit/SWP391_SE2039_G06_SportsCenterManagement_BE.git`.
- FE: `SWP391_SE2039_G06_SportsCenterManagement_FE`, nhánh `feature/fe-fixes-and-support`, remote `https://github.com/DuyPham-bit/SWP391_SE2039_G06_SportsCenterManagement_FE.git`.
- Chưa commit, push, merge hoặc triển khai. Áp dụng bằng cách chạy lại backend với source mới và build/chạy frontend mới. Rollback bằng phiên bản code trước bản sửa; không cần rollback schema.
- Không gọi cổng thanh toán thật trong kiểm thử này. Chưa chạy SonarQube/Playwright MCP; sử dụng build, regression HTTP/SQL và Chrome/CDP hiện có làm bằng chứng.
