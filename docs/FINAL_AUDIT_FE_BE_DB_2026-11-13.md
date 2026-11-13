# Audit cuối trước bảo vệ — luồng FE → BE → DB (13/11/2026)

Nhánh: `feature/rbac-admin-manager-integrated` (HEAD `66eb3b0` + 3 file FE chưa commit).
Phương pháp: build/test thật, đối chiếu contract FE↔BE↔DB bằng script, đọc toàn bộ tầng auth/RBAC/booking/entitlement/seed, security review riêng cho auth/payment/upload/IDOR.

## Repo làm gì, tải giả định

Hệ thống quản lý trung tâm thể thao: hội viên mua gói (VNPay/MoMo/quầy), đặt lịch buổi tập, điểm danh; lễ tân/quản lý/admin vận hành theo từng trung tâm. React 18 + Vite (Vercel) gọi ASP.NET Core .NET 10 (Railway) qua cookie HttpOnly chọn theo tab, lưu PostgreSQL (EF Core).
Tải giả định: một trung tâm vài trăm hội viên, vài chục request đồng thời lúc cao điểm, một instance backend. Các đánh giá "scale" dưới đây theo mức này.

## Kết quả kiểm tra tự động

| Kiểm tra | Kết quả |
|---|---|
| `dotnet build` API | PASS, 0 warning, 0 error |
| `dotnet ef migrations has-pending-model-changes` | PASS, model khớp migration (2 migration: InitialPostgres, PackageEntitlements) |
| `npm test` (Frontend, node --test) | PASS 163/163 |
| `npm run build` (vite) | PASS, cảnh báo chunk > 1 MB (pdfmake, exceljs, vfs_fonts) |
| Contract FE→BE: 155 lời gọi `apiRequest` so với 199 route BE | PASS, mọi lời gọi đều có route tương ứng (13 cảnh báo của script đều là template string, đã kiểm tay) |
| LogicAudit (154 ca BE, cần PostgreSQL) | KHÔNG CHẠY ĐƯỢC: Docker Desktop không bật, cổng 5432 đóng |

## Phải sửa (Must fix) — ĐÃ SỬA 13/11/2026 (chưa commit)

Trạng thái sau khi sửa: mục 1 → `DbInitializer.SeedAsync(context, seedDemoData)` chỉ chạy seed demo khi `Database:SeedDemoData=true` (README đã sửa); mục 2 → bỏ fallback, thiếu `Jwt:Key` thì API dừng khởi động; mục 3 → `JwtAccountValidator` từ chối token của tài khoản `MustChangePassword` trừ `GET profile/me`, `POST auth/initial-password`, `POST auth/logout`, thêm rate limit `login` 20 lần/phút/IP và `ForwardLimit = 1` để IP không bị giả qua `X-Forwarded-For`. Build API + LogicAudit 0 lỗi. Chưa chạy được LogicAudit (cần PostgreSQL).

1. **Seed dữ liệu demo vẫn chạy ở production mỗi lần khởi động** (`Backend/SportsCenterManagement.DAL/Context/DbInitializer.cs:13-22`, `:522-1010`)
   - **Đây là gì:** `DbInitializer.SeedAsync` được gọi trong `Program.cs` khi `Database:AutoMigrateOnStartup` (mặc định true). Bước cuối `EnsureDemoBusinessDataAsync` tạo gói, lớp, buổi học, rồi gán cho **mọi hội viên đang Active trong toàn hệ thống** 2 subscription (1 Expired, 1 Active), kèm hóa đơn `Paid` và payment `Succeeded` với mã giao dịch bịa.
   - **Vấn đề:** Chỉ chặn bằng marker `seed.demo.business-data.v2` trong `system_settings`; không có cờ môi trường. DB production mới (hoặc đổi marker version) sẽ bị bơm doanh thu giả và hội viên thật được tặng gói Active miễn phí. README gốc ghi "Mã seed dữ liệu nghiệp vụ đã được gỡ" → hội đồng đối chiếu sẽ thấy sai.
   - **Sửa:** Bọc lời gọi bằng cờ `Database:SeedDemoData` mặc định `false` (1 dòng trong `SeedAsync`), hoặc xóa hẳn hàm (~490 dòng). Sửa README cho đúng.
   - **Nếu bỏ qua:** Báo cáo doanh thu/khách hàng trên môi trường demo không phản ánh giao dịch thật; hội viên thật có gói không trả tiền.

2. **JWT có khóa ký mặc định nằm trong source** (`Backend/SportsCenterManagement.API/Authentication/JwtTokenIssuer.cs:18`)
   - **Đây là gì:** Nếu thiếu `Jwt:Key`, code dùng chuỗi `scms-dev-jwt-key-2026-strong-random-secret-value!`.
   - **Vấn đề:** Chuỗi này công khai trên GitHub. `appsettings.json` không có `Jwt:Key` và `appsettings.Production.json` cũng không, nên production chỉ an toàn nếu Railway đặt biến `Jwt__Key`. Môi trường nào quên đặt `Jwt__Key` (Railway không có file `.env` của docker-compose) thì bất kỳ ai cũng tự ký được token Admin. README và `docs/security-config-mail-bootstrap.md` khẳng định "không có JWT secret mặc định" → mâu thuẫn tài liệu.
   - **Sửa:** Xóa fallback, ném `InvalidOperationException` khi thiếu key (app không khởi động được thay vì chạy với khóa công khai). Kiểm tra Railway đã đặt `Jwt__Key`.
   - **Nếu bỏ qua:** Một cấu hình thiếu biến là mất toàn bộ xác thực.

3. **Tài khoản tạo tại quầy dùng số điện thoại làm mật khẩu, và rào `MustChangePassword` không phủ hết API** (`Backend/SportsCenterManagement.BLL/Services/MemberActivationService.cs:58-69`, `AuthService.cs:162-167`, `API/Authorization/PermissionAuthorizationHandler.cs:29-34`, `API/Controllers/AuthController.cs:72-98`)
   - **Đây là gì:** Lễ tân tạo nhanh hội viên → user có `Username = phone`, `PasswordHash = Hash(phone)`, `MustChangePassword = true`. Login nhận số điện thoại làm định danh. Ý định: hội viên đăng nhập `phone/phone` rồi bị ép đổi mật khẩu.
   - **Vấn đề:** Việc "ép đổi" chỉ được kiểm trong `PermissionAuthorizationHandler`, tức chỉ endpoint có `[RequirePermission]`. Mọi endpoint `[Authorize]`, `[Authorize(Roles=...)]`, `[CenterCapability]` vẫn mở. Kẻ biết số điện thoại hội viên (hoặc quét dải `09xxxxxxxx` với `login = password = phone`; `POST /api/auth/login` không có rate limit theo IP, khóa 5 lần chỉ tính theo từng tài khoản) đăng nhập được, rồi: gọi `POST /api/auth/initial-password` với `CurrentPassword = phone` để đổi mật khẩu và chiếm tài khoản; gọi `POST /api/auth/google/link` gắn Google của mình; gọi `POST /api/payments/{id}/refund-requests` điền số tài khoản ngân hàng của mình cho gói hội viên đã mua tại quầy.
   - **Sửa:** (1) Chặn ở tầng chung: trong `JwtAccountValidator.ValidateAsync` đọc thêm `MustChangePassword`, nếu true và đường dẫn không thuộc `{auth/initial-password, auth/logout, profile/me}` thì `context.Fail(...)`; khoảng 6 dòng, bỏ được check riêng trong handler. (2) `initial-password` đòi thêm activation token/OTP email thay vì chỉ `CurrentPassword`. (3) Thêm `[EnableRateLimiting]` theo IP cho `login` (policy `password-reset` đã có sẵn). Về lâu dài: sinh mật khẩu tạm ngẫu nhiên gửi email/SMS thay vì dùng SĐT.
   - **Nếu bỏ qua:** Chiếm tài khoản và chuyển hướng hoàn tiền chỉ bằng một số điện thoại; đây là câu hỏi bảo mật hội đồng dễ đặt nhất với luồng "đăng ký tại quầy".

### Security review riêng (auth / payment / upload / IDOR) — đã kiểm và đạt

- **VNPay:** HMAC-SHA512 so sánh thời gian cố định, bỏ `vnp_SecureHash` khỏi chuỗi ký, kiểm `vnp_TmnCode`, tiền tệ, `amount == payment.Amount`, `TxnRef` → đúng payment, máy trạng thái chống replay; secret rỗng thì từ chối (`PaymentService.cs:168-172`). Refund response cũng xác minh chữ ký + TxnRef + amount.
- **MoMo:** HMAC-SHA256 trên đúng tập field, kiểm `partnerCode`, `requestId == GatewayReference`, amount; thiếu key → invalid; URL tạo link phải thuộc `*.momo.vn`.
- **Idempotency:** key bắt buộc, scope `SHA256(actorId:key)`; cùng key khác payload → 409 (`PaymentService.cs:2162-2170`).
- **IDOR:** hóa đơn (chủ sở hữu/nhân viên cùng trung tâm/admin), chứng từ hoàn tiền, duyệt hoàn tiền (`EnsureCenterScope`), hủy pending, booking (chủ sở hữu + `CenterScopeService` cho nhân viên), members/staff/checkin/support/audit (kiểm nhân viên trung tâm), hội thoại AI (`UserId == CurrentUserId`), endpoint HLV (qua `ClassCoaches`). Không tìm thấy lỗ hổng.
- **Google login:** kiểm audience = `Google:ClientId`, issuer Google, RS256, `email_verified`; không tự liên kết theo email trùng.
- **OTP/activation:** OTP từ `RandomNumberGenerator`, băm bcrypt, 5 phút, tối đa 5 lần, transaction Serializable; token kích hoạt 32 byte random, lưu SHA-256, 24 giờ.
- **Upload R2:** allowlist đuôi file + Content-Type + magic bytes; key do server sinh (GUID) nên không path traversal; presign/delete regex `^(refund-bills|dashboard|media|avatars)/...` và chặn `..`; avatar phải bắt đầu `avatars/{userId}/`.
- **SQL:** không có `FromSqlRaw`/`ExecuteSqlRaw`; chỉ 2 câu lệnh hằng (advisory lock) trong `Program.cs`.
- **Leo thang quyền:** chỉ Admin sửa ma trận quyền hệ thống; `roles.permissions.manage` không gán được cho role khác; override theo trung tâm chỉ thu hẹp; `StaffManagementService.cs:131-133` chặn bổ nhiệm Admin/Manager.
- **Dưới ngưỡng, ghi nhận:** `X-Forwarded-For` được tin tuyệt đối (`Program.cs:191-196`, `KnownProxies.Clear()`), nên rate limit theo IP có thể bị giả header; sửa bằng `ForwardLimit = 1` hoặc khai báo proxy Railway/Cloudflare. Mọi Manager sửa được nội dung landing toàn hệ thống dù `manage_landing_content` được xếp là capability Admin (có vẻ chủ ý). `request-email-verification` trả 409 lộ email/SĐT đã đăng ký (commit `9d60e61` ghi là quyết định sản phẩm).

## Nên sửa (Should fix)

4. **Tắt capability cho Manager chỉ ẩn menu, API vẫn mở** (`Backend/SportsCenterManagement.API/Controllers/ReportsController.cs:11`, `ClassManagementController.cs:12`, `LandingContentController.cs:21`, `R2ImageStorageController.cs`, `RefundBillStorageController.cs`)
   - **Đây là gì:** Admin có thể tắt từng capability (`view_reports`, `manage_classes`, `assign_coach`, `manage_landing_content`) cho một Manager qua `PUT centers/{id}/user-permissions`. FE đọc `access/me/capabilities` để ẩn menu.
   - **Vấn đề:** 27 endpoint trong 5 controller trên chỉ dùng `[Authorize(Roles = "MANAGER,ADMIN")]`, không có `[CenterCapability(...)]`. Manager bị tắt `view_reports` vẫn gọi `GET /api/reports/...` được 200. Nhóm Member/Coach/Receptionist và các màn dùng permission code (staff, members, packages, audit log) thì BE kiểm thật.
   - **Sửa:** Thêm `[CenterCapability("view_reports")]`, `("manage_classes")`, `("manage_landing_content")` ở cấp class cho 5 controller; mỗi chỗ 1 dòng, attribute đã có sẵn.
   - **Nếu bỏ qua:** Màn "Vai trò & Phân quyền" hứa một điều BE không giữ; hội đồng hỏi "tắt quyền rồi gọi API thì sao" sẽ lộ ngay.

5. **FE có 2 capability BE không biết** (`Frontend/src/entities/permission/model/permissions.js`: `coach_support`, `member_support`)
   - **Đây là gì:** Danh sách capability FE có 24 id; BE baseline (`CenterRolePolicy.Capabilities`) có 22.
   - **Vấn đề:** Hai id này không bao giờ xuất hiện trong `access/me/capabilities`, nên nếu màn nào dùng `requiredCapability` với chúng sẽ luôn bị chặn; nếu không dùng thì là mã chết gây hiểu nhầm trên ma trận quyền.
   - **Sửa:** Xóa 2 mục khỏi FE, hoặc thêm vào baseline MEMBER/COACH và gắn `[CenterCapability]` lên `MySupportRequestsController`.
   - **Nếu bỏ qua:** Ma trận quyền hiển thị toggle không có tác dụng.

6. **Luật đặt lịch viết hai lần, có thể lệch nhau** (`Backend/SportsCenterManagement.BLL/Services/ClassScheduleQueryService.cs:188-200` và `SessionBookingService.cs:243-322`)
   - **Đây là gì:** `ResolveButtonState` tính nút cho FE (`CAN_BOOK`, `SPORT_NOT_INCLUDED`...). `EnsureEligibleAsync` là luật thật khi bấm đặt.
   - **Vấn đề:** ButtonState không xét `MaxClasses` của gói, `BookingSuspendedUntil`, HLV chính/phòng/trung tâm còn Active. Ví dụ: gói Basic `MaxClasses = 10`, hội viên đã vào 10 lớp khác → lịch vẫn hiện "Đặt chỗ", bấm thì 403 `ME-05` "Gói tập đã sử dụng hết số lớp". Luật môn (`AllowsSport`) cũng được chép tay lại trong `EnsureEligibleAsync` thay vì gọi chung.
   - **Sửa:** Cho `EnsureEligibleAsync` dùng `MembershipEntitlements.LoadAsync` + `AllowsSport` (xóa khối chép tay ~40 dòng), và truyền số lớp đã dùng vào `ResolveButtonState` để trả `CLASS_LIMIT_REACHED`.
   - **Nếu bỏ qua:** Demo trước hội đồng có thể gặp nút xanh nhưng đặt bị từ chối.

7. **Monkey-patch `Node.prototype.removeChild/insertBefore` toàn cục** (`Frontend/src/main.jsx:1-24`, chưa commit)
   - **Đây là gì:** Chặn lỗi DOM do Google Translate/extension sửa text node của React.
   - **Vấn đề:** Patch nuốt mọi lỗi `removeChild`/`insertBefore` sai cha, kể cả lỗi thật của ứng dụng; React sẽ tiếp tục với DOM lệch thay vì rơi vào error boundary. Không có test nào bao phủ. Đã có `translate="no"` + `notranslate` trên `<html>` trong cùng diff, đủ cho trường hợp Google Translate.
   - **Sửa:** Bỏ patch, giữ `translate="no"`; nếu vẫn muốn, chỉ bật khi `document.documentElement.classList.contains('translated-ltr')`.
   - **Nếu bỏ qua:** Lỗi render thật bị che, khó chẩn đoán khi demo.

8. **API payroll vẫn mở dù FE đã ẩn vì số liệu sai** (`Backend/SportsCenterManagement.API/Controllers/PayrollController.cs:16-22`)
   - **Đây là gì:** README/commit `f7b0f0f` ghi payroll bị ẩn vì KPI và lương đếm buổi khác nhau, chưa tính hoa hồng.
   - **Vấn đề:** `GET /api/payroll/report` và `POST /api/payroll/staff-policy` vẫn trả dữ liệu cho MANAGER qua Swagger/Postman. Số sai nhưng vẫn ghi được chính sách lương.
   - **Sửa:** Xóa controller (hoặc `[ApiExplorerSettings(IgnoreApi = true)]` + trả 404) cho đến khi hoàn thiện.
   - **Nếu bỏ qua:** Hội đồng mở Swagger thấy endpoint "tính lương" trả số không đúng.

9. **Mật khẩu PostgreSQL local nằm trong file được commit** (`Backend/SportsCenterManagement.API/appsettings.Development.json:3`)
   - **Đây là gì:** Chuỗi kết nối dev kèm password 48 ký tự hex, đã commit từ `1df0951`.
   - **Vấn đề:** Dù chỉ là DB local, file nằm trong git history; nếu cùng password dùng ở nơi khác thì lộ. Cũng trái với quy tắc "không commit secret thật" trong `docs/PROJECT_SUMMARY.md`.
   - **Sửa:** Để trống trong file, dùng `dotnet user-secrets` (đã hướng dẫn sẵn trong README). Đổi password container local.
   - **Nếu bỏ qua:** Hội đồng rà repo thấy password trong source.

10. **CORS cho phép mọi domain `olympic-sports-arena*.vercel.app` kèm credentials** (`Backend/SportsCenterManagement.API/Program.cs:118-125`)
   - **Đây là gì:** Để preview deployment Vercel gọi được API.
   - **Vấn đề:** Bất kỳ tài khoản Vercel nào cũng tạo được project tên `olympic-sports-arena-xyz` → origin đó được `AllowCredentials`. Hiện khai thác bị chặn vì cookie chọn theo header `X-SCMS-Session` 32 hex mà trang lạ không biết, nhưng lớp bảo vệ còn một.
   - **Sửa:** Đặt `Cors:AllowedOrigins` chính xác trên Railway và xóa nhánh `EndsWith(".vercel.app")`.
   - **Nếu bỏ qua:** Nếu sau này đổi cơ chế session, lỗ hổng mở ngay.

## Nên có (Nice to have)

11. **Bundle FE có 3 chunk > 800 KB** (`pdfmake` 1.2 MB, `exceljs` 938 KB, `vfs_fonts` 830 KB). Đã tách chunk riêng nên chỉ tải khi xuất file; nếu muốn tắt cảnh báo build, đặt `build.chunkSizeWarningLimit` hoặc tải `vfs_fonts` theo nhu cầu.

12. **Vị trí hàng chờ tính bằng COUNT tương quan cho từng entry** (`ClassScheduleQueryService.cs:173-178`): O(n²) theo số người chờ mỗi buổi; với vài chục người là không đáng kể, ghi nhận để không mở rộng thêm.

## Đã kiểm và đạt (để trả lời hội đồng)

- **Xác thực:** JWT HS256 pin thuật toán, kiểm issuer/audience/lifetime, `ClockSkew` 30 s; mỗi request tra DB `Status`/`LockedUntil`/`AuthVersion` nên đổi mật khẩu hoặc khóa tài khoản có hiệu lực ngay (`JwtAccountValidator.cs`).
- **Cookie:** HttpOnly, `Path=/api`, `Secure` khi HTTPS, `SameSite=Strict` mặc định; chỉ gửi khi có header `X-SCMS-Session` nên form-CSRF không dùng được cookie (`BrowserSessionCookie.cs`).
- **Đăng nhập:** băm PBKDF2-SHA256 có salt (`PasswordHashing.cs`, còn nhận hash BCrypt cũ để đăng nhập chuyển tiếp), khóa 15 phút sau 5 lần sai, so sánh hash giả khi user không tồn tại để không lộ username (`AuthService.cs:173-190`).
- **RBAC:** `FallbackPolicy` bắt buộc đăng nhập toàn API; 17 endpoint `[AllowAnonymous]` đều là login/google/logout, catalog công khai, landing GET, callback/IPN cổng thanh toán, health. Manager không cấp được quyền Admin: `CenterPermissionsController` chặn `roles.permissions.manage` và chỉ cho override Member/Coach/Receptionist (Admin mới đụng Manager).
- **Dữ liệu tiền và đặt chỗ:** booking, thanh toán, hoàn tiền chạy trong transaction `Serializable`; deadlock/serialization → 503 + `Retry-After`, trùng unique → 409 `COMMON-03` (`Program.cs`). Unique index cho `IdempotencyKey` (payment, invoice, refund), `ProviderTransactionId`, `TransactionCode`, `(SessionId, MemberId)` booking, `InvoiceNumber`.
- **Hàng chờ:** FIFO theo `JoinedAt, Id`; chỗ trống ưu tiên hàng chờ trước người đặt mới (`SessionBookingService.cs:39-43`).
- **Nhắc lịch:** worker 15 s, `ReminderKey` unique nên không gửi trùng khi chạy nhiều instance.
- **Schema:** EF model trùng snapshot migration; không có raw SQL trong BLL/DAL/API (không có `FromSqlRaw`/`ExecuteSqlRaw`).
- **Contract FE↔BE:** toàn bộ 155 lời gọi API trong `Frontend/src` khớp route BE; ButtonState FE (`sessionAction.js`) xử lý đủ 8 trạng thái BE trả về.
- **Múi giờ:** test 5 múi giờ thiết bị PASS; DB lưu UTC, nghiệp vụ theo `Asia/Ho_Chi_Minh`.

## Kết luận

**Verdict:** Sửa mục 1, 2 và 3 trước khi bảo vệ (mục 1, 2 dưới 10 dòng; mục 3 khoảng 20 dòng); mục 4 nên làm vì là điểm hội đồng dễ hỏi nhất về RBAC. Phần còn lại hệ thống chắc, có test và transaction đúng chỗ.
**Lean:** −~530 dòng khả thi (seed demo ~490, luật chép tay trong `EnsureEligibleAsync` ~40), −0 dependency.
**Not checked:** LogicAudit (cần Docker/PostgreSQL), các test `*.browser.cjs` (cần Chrome + API chạy), `ReportService` chi tiết số liệu, `CoachProfessionalService`/`TrainingService`/`PayrollService` nội dung, `GeminiService` prompt, luồng upload R2 thực tế (chỉ đọc code), hành vi production trên Railway/Vercel.

## Checklist 15 phút trước khi bảo vệ

1. Bật Docker Desktop → `start-postgres.bat` → chạy LogicAudit theo `Backend/verification/LogicAudit/README.md`, chụp dòng tổng kết.
2. Kiểm tra Railway có biến `Jwt__Key`, `ConnectionStrings__SportsCenter`, `Bootstrap__Admin__Password`, `Cors__AllowedOrigins`.
3. Commit hoặc bỏ 3 file FE đang sửa dở (`index.html`, `theme.css`, `main.jsx`) để demo đúng bản đã test.
4. Mở Swagger ở môi trường dev, thử gọi 1 endpoint Manager bằng token Member để minh họa 403.
