# Tích hợp API và JWT — 11/10/2026

- Nguồn: yêu cầu nối các API còn thiếu và dùng JWT; người dùng xác nhận bổ sung Google Login và AI.
- Decision: giữ React 18/Vite, ASP.NET Core .NET 10, EF Core và SQL Server.
- Phạm vi: xác thực JWT thống nhất, CORS/Swagger; thay dữ liệu trình duyệt bằng API giáo án, tiến độ, thông báo; Google Identity Services và AI gọi từ backend.
- Auth: kiểm tra chữ ký HS256, issuer, audience, thời hạn; kiểm tra lại tài khoản/role/center từ DB ở mỗi request. Token Data Protection cũ cần đăng nhập lại.
- Ranh giới: HLV chỉ ghi dữ liệu lớp được phân công, Manager xem trong trung tâm; hội viên chỉ đọc dữ liệu của mình. Actor/coach không nhận từ frontend.
- Data: migration bổ sung giáo án chung (member nullable), chi tiết bài tập JSON, chỉ số tiến độ và Google subject có unique index; giữ schema SQL Server hiện có.
- Google: kiểm tra ID token phía server; không tự liên kết tài khoản chỉ vì trùng email. Người dùng đã có tài khoản phải đăng nhập bằng mật khẩu để liên kết.
- AI: provider/model/key cấu hình ngoài mã nguồn; chưa cấu hình trả 503, không trả kết quả giả. Lưu lịch sử theo người dùng và kiểm tra quyền sở hữu hội thoại.
- Lỗi: giữ lỗi HTTP/Problem Details; FE hiển thị lỗi tải thay vì che lỗi thành danh sách rỗng. Không ghi token/key vào log.
- Nhân viên: bổ nhiệm hội viên trong trung tâm, giữ user/member ID và mật khẩu; đổi Coach/Receptionist, không nâng Manager và không đổi HLV còn được phân công lớp. Tìm hội viên qua server, không tìm trong danh sách client rỗng.
- Báo cáo: đếm toàn bộ check-in theo múi giờ báo cáo, không đếm danh sách bị giới hạn 100; xuất CSV Unicode từ API và vô hiệu hóa công thức trong nhãn dữ liệu.
- Kiểm chứng: test hợp đồng FE, build FE/BE, regression backend, HTTP kiểm tra JWT và API trên database kiểm thử riêng nếu môi trường cho phép.
- Constraint: workspace không có metadata Git; không có remote/branch để báo cáo. Không push hoặc release.
- Rollback: hoàn tác code; không chạy migration Down khi đã có giáo án chung hoặc dữ liệu Google mới nếu chưa xuất/đối soát dữ liệu.

## Cấu hình miễn phí cho môn học

Decision: Google Identity Services cho đăng nhập; Gemini 3.5 Flash-Lite Free Tier cho chat/gợi ý giáo án, theo yêu cầu dùng dịch vụ miễn phí và dễ cấu hình. Model 2.5 Flash-Lite trả 404 cho tài khoản mới nên đã thay bằng model Google đề xuất và kiểm chứng lại. Không cần Google client secret cho luồng ID token này. Google OAuth Web Client ID đã được cấu hình; khóa AI người dùng cung cấp được lưu trong User Secrets của backend. Xem [hướng dẫn cấu hình](./free-google-gemini-setup.md).

Constraint: GeminiService đã gọi Gemini thật thành công cho chat và JSON giáo án bằng cấu hình của người dùng; chưa kiểm tra giao diện AI và lưu hội thoại với cấu hình thật, hoặc đăng nhập/liên kết Google bằng tài khoản trình duyệt. Đã kiểm chứng lỗi 503 khi thiếu cấu hình; không thay bằng tài khoản/câu trả lời mô phỏng. Quota/model Free Tier phụ thuộc nhà cung cấp.

## Các API bổ sung

Tất cả đường dẫn dưới đây có tiền tố `/api`. Ngoại trừ `auth/google`, các API yêu cầu Bearer JWT; backend kiểm tra role/capability và phạm vi dữ liệu.

| Method | Đường dẫn | Hành vi |
|---|---|---|
| POST | `auth/google` | Xác minh Google ID token, tạo hội viên mới hoặc đăng nhập tài khoản đã liên kết |
| POST | `auth/google/link` | Liên kết Google cùng email sau khi xác nhận mật khẩu hiện tại |
| POST | `centers/{centerId}/staff/appointments` | Bổ nhiệm hội viên hiện có làm Coach/Receptionist, không tạo lại tài khoản |
| PATCH | `centers/{centerId}/staff/{staffUserId}` | Cập nhật hồ sơ/trạng thái và đổi Coach/Receptionist có kiểm tra phân công |
| GET | `reports/checkins?from=...&to=...` | Đếm check-in trong kỳ theo phạm vi trung tâm và múi giờ báo cáo |
| GET | `coaches/me/classes` | Lớp/học viên được phân công; Manager chỉ xem trong trung tâm |
| GET | `coaches/me/classes/{classId}/members` | Danh sách học viên theo class ID, khác session ID dùng cho điểm danh |
| GET / POST | `coaches/me/training-plans` | Đọc/lưu giáo án chung |
| PUT | `coaches/me/personal-training-plans` | Tạo/cập nhật giáo án cá nhân, không nhân bản giáo án đang hoạt động |
| DELETE | `coaches/me/training-plans/{planId}` | Lưu trữ giáo án, không xóa lịch sử |
| GET / POST | `coaches/me/progress` | Đọc/ghi chỉ số và nhận xét |
| GET | `members/me/training-plans`, `members/me/progress` | Hội viên đọc dữ liệu được phép xem |
| GET / POST | `coaches/me/notifications` | HLV đọc thông báo đã gửi/gửi tới học viên được phân công |
| GET | `notifications/me` | Hộp thư của tài khoản hiện tại |
| PUT | `notifications/{notificationId}/read` | Chỉ người nhận được đánh dấu đã đọc |
| POST | `ai/chat` | Chat Gemini, lưu hội thoại thuộc người dùng |
| GET | `ai/conversations/{conversationId}/messages` | Đọc lịch sử hội thoại thuộc người dùng |
| POST | `ai/recommendations` | Gợi ý JSON cho học viên trong lớp được phân công; bấm áp dụng mới lưu giáo án |

## Bằng chứng kiểm tra ngày 11/10/2026

| Kiểm tra | Lệnh từ workspace root, trừ FE | Kết quả |
|---|---|---|
| FE production build | `npm.cmd run build` trong `Frontend` | Pass; Vite cảnh báo bundle >500 kB và CJS API deprecated |
| FE regression | `npm.cmd test` trong `Frontend` | 23/23 pass |
| BE build | `dotnet build Backend/Backend/SportsCenterManagement.API/SportsCenterManagement.API.csproj --no-restore -o .validation/jwt-api` | Pass, 0 warning / 0 error |
| GeminiService với Gemini thật | Sau khi build BE Release vào `.validation/jwt-api`, chạy `dotnet run --project .validation/gemini-live-smoke/GeminiLiveSmoke.csproj --configuration Release --no-restore` | Pass: model 3.5 Flash-Lite, chat có nội dung, JSON giáo án có đủ trường và 2 bài tập; key đọc từ User Secrets, không ghi database |
| Flow 2 regression | `dotnet run --project Backend/Backend/tests/Flow2.Regression/Flow2.Regression.csproj --no-restore --configuration Release` | 10 validation + 4 capability checks pass |
| JWT, migration, SQL Server, HTTP và browser | `dotnet run --project Backend/Backend/tests/ApiIntegration.Regression/ApiIntegration.Regression.csproj --configuration Release --no-restore -- --sql --browser` | 64 checks pass; bao gồm 1 kiểm tra tổng hợp với 12 kịch bản Chrome |
| Model/migration | `dotnet ef migrations has-pending-model-changes --project SportsCenterManagement.DAL --startup-project SportsCenterManagement.API --configuration Release --no-build` trong `Backend/Backend` | Không còn thay đổi model chưa có migration |

HTTP kiểm tra chữ ký/issuer/audience/expiry/algorithm, từ chối token cũ, tài khoản bị khóa sau đăng nhập, phạm vi HLV/hội viên, lưu/đọc dữ liệu, upsert giáo án, thông báo và quyền sở hữu, CORS, Swagger, Google/AI thiếu cấu hình. Fixture HLV có cả StaffProfile để kiểm tra không vô tình nhận quyền xem của Manager.

Kiểm tra bổ nhiệm/đổi role gồm chặn hội viên tự bổ nhiệm, trung tâm/hội viên ngoài phạm vi, nâng Manager, trùng bổ nhiệm, giữ ID/mật khẩu/lịch sử, role DB có hiệu lực trên JWT đang dùng, lưu lịch sử Coach khi chuyển Receptionist và chặn HLV còn phân công. Báo cáo đếm đủ 101 lượt, bỏ lượt trước mốc ngày Việt Nam và bỏ qua centerId ngoại phạm vi do Manager gửi.

Chrome kiểm tra đăng nhập/đăng xuất thật, HLV tạo giáo án và gửi thông báo, hội viên đọc giáo án/tiến độ/hộp thư, lỗi cấu hình Google/AI, Manager tìm/bổ nhiệm hội viên, đổi Receptionist sang Coach và tải CSV có số check-in từ server; không có JavaScript exception hay request nghiệp vụ thất bại ngoài 503 AI dự kiến. Không có Playwright khả dụng; dùng Chrome DevTools Protocol trực tiếp, không tuyên bố đã chạy Playwright/SonarQube.

Artifacts local: `.validation/browser/report.json`, `requests.json`, `coach-training-plan-desktop.png`, `member-progress-mobile.png`, `manager-report-desktop.png`, `SCMS-report-MONTH.csv`, `.validation/api-integration-runtime.log`, `.validation/gemini-live-smoke/result.json`. Database kiểm thử cuối: `SCMS_ApiIntegration_Test_121467cf70bb4ae3a25e1c408f39d692`. Mỗi lần chạy tạo database riêng; chưa chạy migration hoặc ghi dữ liệu vào database ứng dụng của người dùng. Các database kiểm thử được giữ để đối soát, không tự xóa dữ liệu.

Trong kiểm thử clean database, phát hiện seed trùng email hội viên demo; đã sửa username tham chiếu đúng tài khoản hiện có. Migration `20261011055521_CompleteApiIntegration` áp dụng thành công từ schema ban đầu. Down chặn rollback khi còn giáo án chung có member null; xuất dữ liệu và kiểm tra trước rollback.

## Giới hạn bàn giao

- Khởi động lại backend để áp dụng migration vào database cấu hình của ứng dụng; đăng nhập lại vì token cũ không còn hợp lệ. Khởi động lại Vite sau khi đổi `.env.local`.
- Google Login/liên kết và thanh toán qua nhà cung cấp chưa được kiểm chứng bằng cấu hình của người dùng. Gemini đã kiểm chứng ở tầng service, chưa kiểm tra giao diện và lưu hội thoại với cấu hình thật.
- Module hỗ trợ hội viên chưa có giao diện/API; reset toàn bộ dữ liệu không có API và service báo lỗi rõ ràng.
- Kiểm thử SQL/HTTP chạy với seed Development, đúng môi trường local của bài môn học. Khi dùng database legacy hoặc Production, System Admin cần kiểm tra/cấp ma trận permission của Manager/Receptionist; không tự ghi đè cấu hình quyền đang có.
- Không có metadata Git hoặc MCP quality gate trong workspace; không tạo commit, PR, push, merge hoặc release. Thay đổi và bằng chứng được bàn giao local.
