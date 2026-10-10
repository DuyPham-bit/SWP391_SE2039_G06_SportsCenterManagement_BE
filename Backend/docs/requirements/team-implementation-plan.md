# Kế hoạch chia code theo vai trò và nghiệp vụ

**Trạng thái:** Draft để nhóm thống nhất trước khi triển khai.  
**Kiến trúc:** ASP.NET Core MVC API — Controller nhận/trả HTTP; Service xử lý nghiệp vụ; EF Core DbContext truy cập SQL Server; DTO làm hợp đồng API. Backend không dùng Razor Views vì frontend được tách riêng.

## 1. Phạm vi tổng hợp

### MVP bắt buộc

1. User and membership management.
2. Class booking and schedule management.
3. Payment and report management.

### Chức năng theo vai trò cần đưa vào backlog

- Center Manager: nhân sự/thành viên, lớp/bộ môn/phòng/lịch/coach, gói, role/permission, dashboard và audit log.
- Coach: lịch dạy, roster, thông tin/mục tiêu thành viên, training plan, kết quả, progress review, attendance, assignment/homework, AI gợi ý bài tập.
- Member: hồ sơ, gói, lớp/lịch, booking/hủy, lịch sử attendance/kết quả, plan/review, AI Q&A, notification.
- Receptionist: tìm kiếm/tạo member, bán/gia hạn gói, check-in, ghi danh/hủy lớp, thu tiền/hóa đơn, support request.

## 2. Chia việc đề xuất

| Người | Module sở hữu | Chức năng chính | File/folder độc lập |
|---|---|---|---|
| **Duy** | Account, Member & Membership | Đăng ký/đăng nhập, hồ sơ thành viên, role/permission nền, danh sách thành viên, gói và subscription, thao tác bán/gia hạn gói tại quầy | `API/Controllers/AuthController.cs`, `MembersController.cs`, `MembershipPackagesController.cs`; `Services/Features/Membership/`; `API/Contracts/Membership/` |
| **Huy** | Class, Schedule & Coaching | Bộ môn/lớp/phòng/lịch, phân công coach, booking/enrollment/waitlist, lịch coach/member, điểm danh, training plan/result/review, assignment/homework | `API/Controllers/ClassesController.cs`, `SchedulesController.cs`, `CoachController.cs`, `TrainingController.cs`; `Services/Features/Classes/`, `Services/Features/Training/`; `API/Contracts/Classes/`, `Training/` |
| **Thịnh** | Reception, Payment, Reports & Operations | Check-in tại trung tâm, invoice/payment/refund, báo cáo doanh thu/lớp/thành viên, support requests, notification, audit log; điều phối các phần ghép cuối | `API/Controllers/CheckinsController.cs`, `PaymentsController.cs`, `ReportsController.cs`, `SupportRequestsController.cs`, `NotificationsController.cs`, `AuditLogsController.cs`; `Services/Features/Payments/`, `Reports/`, `Operations/` |

### AI

Để không chặn ba flow bắt buộc, Huy tạo interface/service boundary cho AI sau khi training plan/exercise có contract ổn định. Chỉ tích hợp provider thật sau khi nhóm chốt provider, API key/config, giới hạn chi phí và dữ liệu được gửi đi. Thịnh có thể hỗ trợ lưu conversation/notification nếu cần. Không để AI trực tiếp ghi training plan hoặc thay dữ liệu nghiệp vụ nếu chưa có Coach/Member xác nhận.

### File dùng chung — chỉ định một người tích hợp

Đề xuất **Thịnh làm integration owner**, nhưng không sở hữu nghiệp vụ của hai người còn lại:

- Chỉ integration owner sửa `Program.cs`, `SportsCenterDbContext.cs`, entity dùng chung khi cần, migration và SQL schema.
- Mỗi người báo DbSet/quan hệ/index cần thêm bằng một danh sách; không tự tạo migration song song.
- Thịnh tạo **một migration tích hợp** sau khi Duy/Huy hoàn tất thay đổi model.
- Tránh đặt nghiệp vụ của cả ba vào `CoreFlowService.cs`; tách thành service theo feature.

## 3. Luồng tích hợp bắt buộc

```text
Duy: member + package
      └── tạo MemberSubscription PendingPayment + Invoice/InvoiceItem
              └── Thịnh: nhận payment, cập nhật invoice
                      └── khi đủ tiền: kích hoạt subscription
                              └── Huy: chỉ cho booking nếu subscription Active/còn hạn
```

Hợp đồng giữa các module:

- Duy tạo subscription ở `PendingPayment`, chưa cho sử dụng quyền lợi.
- Thịnh cập nhật payment/invoice trong một transaction; khi paid đủ mới kích hoạt subscription liên quan.
- Huy kiểm tra subscription `Active`, ngày hiệu lực, center và quyền gói trước khi ghi danh.
- Receptionist ghi danh/đăng ký gói phải lưu user thực hiện vào `RegisteredBy`, `CreatedBy` hoặc `ProcessedBy` phù hợp.
- Actor/role/center scope lấy từ claims của phiên đăng nhập, không lấy `UserId`, role hoặc center quyền hạn từ body client.

## 4. Flow 1 — User and Membership (Duy; Thịnh hỗ trợ thu tiền)

### API đề xuất

```text
POST  /api/auth/register
POST  /api/auth/login
GET   /api/members/me
PATCH /api/members/me
GET   /api/centers/{centerId}/members?query=
POST  /api/centers/{centerId}/members              # Receptionist tạo tại quầy
GET   /api/centers/{centerId}/membership-packages
POST  /api/members/me/subscriptions
POST  /api/members/{memberId}/subscriptions        # Receptionist/Manager
GET   /api/members/{memberId}/subscriptions
POST  /api/centers/{centerId}/membership-packages  # Manager
PATCH /api/membership-packages/{packageId}          # Manager
```

### Happy case

1. Member đăng ký với dữ liệu hợp lệ; backend hash password, tạo User với role Member và MemberProfile.
2. Receptionist tra cứu trước; nếu chưa tồn tại thì tạo đúng một hồ sơ.
3. Manager tạo package với giá dương, thời hạn dương, center hợp lệ và status Draft/Active theo quyền.
4. Member chọn package Active; service chụp giá/thời hạn tại thời điểm mua, tạo PendingPayment subscription, invoice và invoice item.
5. Thịnh thu đủ tiền; subscription chuyển Active và có ngày bắt đầu/kết thúc.
6. Member và receptionist xem được trạng thái/gói/lịch sử phù hợp với quyền của họ.

### Unhappy case

| Tình huống | Hành vi |
|---|---|
| Username/email trùng | `409`; không tạo nửa User/Profile. |
| Validation/password policy lỗi | `400` theo field; password không bao giờ trả ra response/log. |
| Gói inactive, hết hiệu lực bán hoặc khác center | Từ chối; không tạo invoice/subscription. |
| Member bị khóa hoặc tài khoản chưa xác minh theo policy | Từ chối đăng nhập/mua gói theo policy. |
| Mua lặp do retry | Idempotency/duplicate check; không có hai hóa đơn ngoài ý muốn. |
| Lỗi giữa tạo subscription và invoice | Rollback toàn bộ transaction. |
| Member đổi ID trên URL để xem hồ sơ người khác | `403/404`; kiểm tra quyền bằng claims và relationship. |
| Gia hạn chồng ngày hoặc không xác định ngày hiệu lực | Áp dụng policy đã chốt; không tự để subscription overlap. |

### DB hiện có và việc cần rà soát

`User`, `Role`, `Permission`, `RolePermission`, `MemberProfile`, `MembershipPackage`, `MemberSubscription` đã có. `User.RoleId` hiện biểu diễn một role/user; nếu người dùng được kiêm nhiều role/center thì cần quyết định đổi sang bảng liên kết. Duy cần dùng password hash hiện có, không tự lưu mật khẩu rõ.

## 5. Flow 2 — Class Booking and Schedule (Huy)

### API đề xuất

```text
GET    /api/centers/{centerId}/sports
GET    /api/centers/{centerId}/rooms
POST   /api/classes                                  # Manager
PATCH  /api/classes/{classId}                        # Manager
POST   /api/classes/{classId}/schedules              # Manager
POST   /api/classes/{classId}/coaches                # Manager
POST   /api/classes/{classId}/publish                # Manager
GET    /api/centers/{centerId}/classes?from=&to=
GET    /api/coaches/me/schedule?from=&to=
POST   /api/classes/{classId}/enrollments            # Member/Receptionist
DELETE /api/classes/{classId}/enrollments/{id}
GET    /api/classes/{classId}/roster                 # Coach được phân công
```

### API đã triển khai cho FR-2.1 đến FR-2.3

```text
GET    /api/centers/{centerId}/sports                 # Bộ môn đang hoạt động
GET    /api/centers/{centerId}/rooms                  # Phòng đang hoạt động của cơ sở
POST   /api/classes                                    # Manager; tạo lớp Draft
POST   /api/classes/{classId}/schedules                # Manager; tạo lịch và các ClassSession
POST   /api/classes/{classId}/coaches                  # Manager; endpoint hiện có, Coach chính được lưu vào các buổi tương lai
POST   /api/classes/{classId}/publish                  # Manager; công bố lớp sau khi có lịch và Coach chính
GET    /api/centers/{centerId}/classes                 # Member/Manager/Admin/Receptionist; danh sách lớp Published
GET    /api/class-sessions?centerId=&sportId=&coachId=&from=&to=&page=&pageSize= # Buổi theo center
GET    /api/coaches/me/teaching-schedule?from=&to=     # Coach; lịch dạy của chính mình
GET    /api/coaches/me/class-sessions/{sessionId}/roster # Coach được gán; roster buổi
POST   /api/class-sessions/{sessionId}/bookings        # Member đặt một buổi
POST   /api/class-sessions/{sessionId}/members/{memberId}/bookings # Receptionist đặt hộ tại center được gán
GET    /api/members/me/session-bookings                # Member; booking của chính mình
DELETE /api/session-bookings/{bookingId}               # Member; hủy booking của chính mình
POST   /api/classes/{classId}/enrollments              # Disabled; returns 409 to prevent class/session double counting
```

Đăng ký mới chỉ dùng `SessionBooking`: Member lấy từ JWT; Receptionist chỉ đặt hộ tại center được phân công. Subscription phải `Active`, còn hạn, cùng center, đủ `MaxClasses`/`AllowedSports`; lớp phải Published. Buổi đầy tạo waitlist theo session. Hủy trước hạn 2 giờ theo UTC+7 giải phóng chỗ và promote người chờ ở đúng buổi; hủy muộn chuyển `CANCELLED_LATE_CHARGED`, giữ chỗ đã tính phí và không promote. Lớp mới ở trạng thái Draft; Manager/Admin cần tạo lịch có session tương lai và gán Coach Active cùng center rồi mới công bố. `centerId` bắt buộc cho danh sách buổi; thao tác Manager được kiểm tra center theo StaffProfile (Admin toàn cục). Ngày trong tuần dùng quy ước .NET: Chủ nhật `0`, Thứ hai `1`, …, Thứ bảy `6`.

Payload tạo lớp:

```json
{
  "centerId": 1,
  "sportId": 2,
  "roomId": 3,
  "name": "Yoga cơ bản",
  "description": "Lớp nhập môn",
  "level": "Beginner",
  "capacity": 20,
  "durationMinutes": 60
}
```

Payload tạo lịch:

```json
{
  "roomId": 3,
  "dayOfWeek": 1,
  "startTime": "18:00:00",
  "endTime": "19:00:00",
  "startDate": "2026-10-05",
  "endDate": "2026-12-28"
}
```

Tạo lớp/lịch trả `201`; đặt thành công trả `201`; vào waitlist trả `202`; hủy booking trả `200`. Endpoint thao tác lớp yêu cầu `MANAGER` hoặc `ADMIN`; danh mục/buổi yêu cầu JWT, member đặt cho mình và Receptionist đặt hộ. Endpoint ghi danh cả lớp trả `409` vì luồng này đã đóng cho đăng ký mới. Payload sai trả `400`, chưa xác thực `401`, sai role `403`, lớp draft/booking trùng `409`.

### Quyết định MVP

Đăng ký theo từng buổi (`SessionBooking`) là nguồn sự thật duy nhất cho đăng ký mới. `POST /classes/{classId}/enrollments` bị vô hiệu hóa; danh sách/hủy `ClassEnrollment` chỉ còn để xử lý dữ liệu lịch sử. Mỗi waitlist gắn với `SessionId`, có unique filtered index cho member đang chờ cùng buổi. Chỗ cuối được khóa bằng row lock trong transaction SQL Server; test SQL Server chạy hai request tranh chỗ và hai request cùng member vào waitlist.

### Happy case

1. Manager tạo class Draft với center, sport, room, capacity và duration hợp lệ.
2. Tạo lịch có khoảng ngày hiệu lực, giờ kết thúc sau giờ bắt đầu; phân công coach.
3. Service phát hiện không có xung đột và sinh sessions cụ thể; Manager publish lớp.
4. Member có subscription Active, đúng center, còn hạn và đủ quyền chọn lớp.
5. Service kiểm tra booking chưa tồn tại, còn chỗ, ghi `SessionBooking` cùng subscription và actor; nếu hết chỗ, ghi waitlist gắn đúng session.
6. Coach chỉ xem lịch của chính mình và roster của session được gán; Receptionist có thể hỗ trợ đặt buổi với actor được lưu.

### Unhappy case

| Tình huống | Hành vi |
|---|---|
| Room/coach trùng lịch | Từ chối tạo/publish và trả thông tin session xung đột. |
| Lớp không Published, hủy hoặc đăng ký đóng | Từ chối ghi danh. |
| Subscription pending/expired/khác center/không đủ quota | Từ chối; không giữ chỗ. |
| Session đầy | Tạo waitlist đúng session và trả `202`; waitlisted không tính vào capacity. |
| Hai request tranh chỗ cuối | SQL Server row lock; tối đa một booking confirmed. |
| Hai request đồng thời cùng member vào waitlist | Tái sử dụng cùng bản ghi chờ; unique index chặn duplicate. |
| Hủy booking muộn | Lưu `CANCELLED_LATE_CHARGED`; không giải phóng chỗ hoặc promote. |
| Hủy schedule/class | Cập nhật booking và waitlist tương lai, thông báo member bị ảnh hưởng. |
| Coach xem lớp ngoài assignment | `403`; không trả roster/member data. |

### DB hiện có và việc cần rà soát

Đã có `Sport`, `Room`, `ClassEntity`, `ClassCoach`, `ClassSchedule`, `ClassSession`, `ClassEnrollment`, `SessionBooking`, `ClassWaitlist`, `Attendance`. Migration Flow 2 bổ sung `SessionId` và unique filtered index cho waitlist, cùng actor/subscription FK của booking. Lịch phải lưu session cụ thể để thay đổi template không làm sai lịch sử.

## 6. Flow 3 — Payment and Report (Thịnh)

### API đề xuất

```text
POST /api/invoices/{invoiceId}/payments             # Cashier thu tiền mặt
POST /api/payment-webhooks/{provider}               # Chỉ khi tích hợp online
GET  /api/invoices/{invoiceId}
GET  /api/reports/revenue?centerId=&from=&to=&groupBy=day|month
GET  /api/reports/membership?centerId=&from=&to=
GET  /api/reports/classes?centerId=&from=&to=
POST /api/payments/{paymentId}/refund               # khi có policy/approval
```

### Happy case

1. Service nạp invoice từ DB, tính số dư còn lại từ các payment thành công.
2. Receptionist thu tiền mặt; lưu amount, method, processedBy, paidAt và transaction code duy nhất.
3. Invoice chuyển PartiallyPaid hoặc Paid dựa trên tổng đã thu.
4. Chỉ khi Paid mới kích hoạt subscription/invoice item entitlement.
5. Manager lọc báo cáo theo center/kỳ; báo cáo đối soát được về invoice/payment.

### Unhappy case

| Tình huống | Hành vi |
|---|---|
| Invoice không tồn tại/khác center | `404/403`; không lộ dữ liệu. |
| Invoice Paid/Voided/Refunded | `409`; không nhận thu mới. |
| Amount <= 0 hoặc vượt số dư | Từ chối. |
| Payment pending/failed | Không cộng doanh thu và không kích hoạt subscription. |
| Callback sai chữ ký, invoice hoặc amount | Từ chối; audit/log không ghi secret. |
| Request/callback lặp | Idempotency/unique provider transaction; không thu hoặc cộng doanh thu hai lần. |
| Timeout không rõ giao dịch thành công hay thất bại | Giữ Pending để đối soát, không phỏng đoán. |
| Refund | Không sửa/xóa giao dịch gốc; lưu giao dịch refund và người duyệt riêng. |

### Công thức báo cáo cần chốt

- Doanh thu gộp: tổng payments Succeeded theo thời điểm `PaidAt` trong kỳ.
- Refund: giao dịch refund được chấp thuận trong kỳ.
- Doanh thu ròng: gộp trừ refund.
- Không tính Pending/Failed. Chốt timezone báo cáo (UTC trong DB, timezone center khi hiển thị).

Đã có `Invoice`, `InvoiceItem`, `Payment`. Model hiện chưa có refund transaction riêng; chưa nên hiển thị báo cáo refund/net như thể chức năng đã hoàn thiện.

## 7. Nghiệp vụ vai trò mở rộng

### Coach — do Huy sở hữu

| Nghiệp vụ | Entities nền | Happy case | Unhappy case |
|---|---|---|---|
| Xem lịch/roster | ClassCoach, ClassSession, ClassEnrollment | Coach thấy buổi và member thuộc lớp được gán | Không được xem lớp ngoài assignment; lớp hủy không hiện như buổi đang dạy |
| Xem member basics/goals | MemberProfile | Coach xem dữ liệu tối thiểu phục vụ huấn luyện | Không trả medical note/PII nếu không có quyền/cần thiết |
| Tạo plan cá nhân/lớp | TrainingPlan, TrainingPlanExercise | Coach tạo plan gắn member hoặc class và exercise hợp lệ | Coach không phụ trách member/lớp; exercise inactive; thiếu mục tiêu/plan dates |
| Ghi kết quả và review | TrainingResult, MemberProgressReview | Lưu kết quả gắn coach/member/session và timestamp | Session không thuộc coach; giá trị âm/vượt range; sửa review cũ trái policy |
| Điểm danh | Attendance | Mỗi member có một trạng thái attendance/session | Member không có booking/enrollment; duplicate attendance; session đã khóa |
| Homework | Assignment, AssignmentSubmission | Coach gán bài cho lớp/member; member nộp bài | Coach không phụ trách; quá hạn; nộp thay member khác |
| AI bài tập | AiConversation, AiMessage, AiExerciseRecommendation | Trả gợi ý theo mục tiêu/level/history và lưu recommendation | Provider lỗi; output không hợp lệ; member không đồng ý; không ghi kế hoạch tự động |

### Member — Duy sở hữu hồ sơ/gói; Huy sở hữu lịch/tập luyện

| Nghiệp vụ | Happy case | Unhappy case |
|---|---|---|
| Cập nhật hồ sơ | Chỉ cập nhật field cho phép của chính mình | IDOR, sai định dạng, sửa role/status trực tiếp → chặn |
| Xem lịch/book/hủy | Chỉ hiển thị booking và coach hợp lệ của member | Gói hết hạn, lớp đầy, deadline hủy qua, double booking → từ chối rõ |
| Xem attendance/results/plan/review | Chỉ xem lịch sử của bản thân | Truy cập member khác/coach khác → 403/404 |
| Hỏi AI | Lưu conversation/message; trả lời câu hỏi nằm trong phạm vi dữ liệu | Provider down/rate limit, prompt chứa dữ liệu nhạy cảm, câu hỏi ngoài phạm vi → fallback/giới hạn |
| Nhận notification | Mark read và xem thông báo thuộc chính user | Đọc/sửa notification người khác → từ chối |

### Center Manager — chia theo module owner

- Member/nhân sự/package/role: Duy.
- Class/sport/room/schedule/coach assignment: Huy.
- Dashboard/audit/support/notification policy và quyền xem báo cáo: Thịnh.
- Xóa dữ liệu nghiệp vụ nên dùng soft delete/status để giữ invoice, attendance, audit và lịch sử.
- Thao tác đổi role, disable user, sửa package price, publish/cancel schedule cần ghi `AuditLog`.

### Receptionist — chia theo nghiệp vụ

- Tìm kiếm/tạo member và bán/gia hạn gói: Duy.
- Hỗ trợ ghi danh/hủy lớp: Huy.
- Check-in trung tâm, thu tiền/hóa đơn, support request: Thịnh.
- Receptionist chỉ hoạt động trong center được gán; mọi thao tác có actor id và thời điểm.

### Notifications, Support, Audit — Thịnh sở hữu

Entities nền hiện có `Notification`, `UserNotification`, `SupportRequest`, `SupportRequestMessage`, `AuditLog`.

- Notification là nội dung/thông báo; `UserNotification` là quan hệ nhận và read state.
- Support request có member, người xử lý, priority/status; message lưu hội thoại theo request.
- Audit log ghi actor, action, entity type/id, timestamp, dữ liệu cũ/mới đã lọc PII; không ghi password/token/secret.
- Unhappy cases: gửi notification lặp, user không phải recipient, support đã đóng, audit payload quá lớn hoặc chứa secret.

## 8. MVC code convention và lỗi API

- Controller: nhận DTO, gọi service, trả `ActionResult<T>`; không chứa business rules/EF query.
- Service: validation nghiệp vụ, transaction/concurrency và domain status.
- Entity: schema/persistence; DTO: input/output API, không trả entity EF trực tiếp.
- API error: `400` validation; `401` chưa đăng nhập; `403` không đủ quyền; `404` không tìm thấy; `409` trạng thái/capacity/duplicate conflict; `500` lỗi không dự kiến với correlation id.
- Mọi endpoint ghi dữ liệu phải có authentication/authorization. Hiện repo mới có GET danh mục và chưa cấu hình auth; không expose mutation endpoints cho frontend trước khi hoàn thành.

## 9. Thứ tự làm để giảm phụ thuộc

1. Cả nhóm thống nhất status, DTO, policy ngày hết hạn, class enrollment vs session booking, và cách thu tiền.
2. Duy làm auth/member/package/subscription contract; Huy làm catalog/lịch lớp độc lập; Thịnh chuẩn bị payment/report contract.
3. Duy và Thịnh ghép invoice → payment → subscription activation.
4. Huy nối điều kiện subscription Active vào enrollment và hoàn thiện coach tools.
5. Thịnh nối check-in, dashboard, support, notification, audit.
6. AI làm sau khi training/exercise contracts ổn định và nhóm quyết định provider/secret management.
7. Integration owner cập nhật migration/schema một lần; ghép controller DI; build solution.

## 10. Definition of Done cho mỗi task

- Route, role được phép và center scope được nêu rõ.
- Happy path và các lỗi validation/permission/duplicate/conflict đã được xử lý.
- Không nhận giá, actor id, role hoặc entitlement đáng tin từ client body.
- Thay đổi ghi nhiều bảng được transaction bao quanh.
- Có audit cho thao tác quan trọng và không log secret/PII không cần thiết.
- DTO không lộ password hash, medical note hoặc thông tin tài chính ngoài quyền.
- Migration chỉ do integration owner tạo; README/API contract được cập nhật.

