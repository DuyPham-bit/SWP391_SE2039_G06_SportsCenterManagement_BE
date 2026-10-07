# Đặc tả nháp: Ba luồng nghiệp vụ bắt buộc

**Trạng thái:** Flow 1 đã được triển khai theo các quyết định ghi dưới đây. Backend đã có API MVP cho Flow 2 và Flow 3; một số chính sách nghiệp vụ còn mở và ma trận case trong tài liệu chưa phải bằng chứng automated test đã pass.
**Phạm vi:** Flow 1 User & Membership Management; Flow 2 Class Booking & Schedule Management; Flow 3 Payment & Report Management.
**Nguồn:** Yêu cầu vai trò Center Manager, Coach, Member, Receptionist do người dùng cung cấp; đối chiếu entities và EF Core DbContext hiện có.

> **Lưu ý kiểm thử:** Các case H/U dưới đây là tình huống và kết quả cần kiểm thử; việc có case trong tài liệu không chứng minh đã có automated test hoặc test đã pass. Các tình huống concurrency/transaction phải được kiểm chứng bằng relational database hỗ trợ transaction; EF InMemory không thay thế được kiểm thử đó.

## 1. Mục tiêu và phạm vi

Hệ thống hỗ trợ quản lý người dùng/thành viên, gói tập, lớp và lịch học, thanh toán và báo cáo doanh thu. Receptionist hỗ trợ thao tác tại quầy; Center Manager quản trị dữ liệu và xem báo cáo. Member tự đăng ký, quản lý gói và đăng ký lớp. Coach xem lớp được phân công và danh sách học viên.

Ngoài phạm vi bắt buộc của tài liệu này: AI, giáo án/tiến độ tập luyện, thông báo và ticket hỗ trợ. DB hiện đã có một số entity nền cho các tính năng này, nhưng chưa đưa vào ba flow bắt buộc.

## 2. Vai trò và quyền trong phạm vi

| Vai trò | Quyền chính |
|---|---|
| Center Manager | Quản lý tài khoản/nhân sự, gói tập, lớp/lịch/phòng/coach; xem báo cáo; xem audit log. |
| Receptionist | Tạo hồ sơ thành viên tại quầy; tra cứu thành viên; bán/gia hạn gói; hỗ trợ ghi danh/hủy lớp; ghi nhận thanh toán; xuất hóa đơn. |
| Member | Đăng ký/cập nhật hồ sơ; xem và mua/gia hạn gói; xem lớp/lịch; đăng ký/hủy theo chính sách; xem trạng thái thanh toán và lịch đăng ký. |
| Coach | Chỉ xem lịch và danh sách lớp được phân công. Coach không được thay đổi gói, thanh toán hoặc dữ liệu thành viên ngoài phạm vi nghiệp vụ đã cấp. |
| System Admin | Quản trị toàn hệ thống; là role duy nhất được xem audit toàn hệ thống và thay ma trận quyền của các role. Không được tạo qua API tạo nhân sự trung tâm. |

**Quy tắc phân quyền nền:** API phải kiểm tra role và phạm vi center cho mọi thao tác; không dựa vào việc frontend ẩn nút. Không trả password hash hoặc dữ liệu nhạy cảm qua API.

**Quyết định RBAC:** Mỗi user có một role. Ma trận `RolePermission` là nguồn cấp quyền cho các API có permission policy. Chỉ System Admin được sửa ma trận; quyền `roles.permissions.manage` luôn thuộc SystemAdmin và không thể cấp cho role khác hoặc tự gỡ. Tài khoản System Admin đầu tiên chỉ được bootstrap bằng biến môi trường `Bootstrap__SystemAdmin__Username`, `Bootstrap__SystemAdmin__Email`, `Bootstrap__SystemAdmin__Password`; không có mật khẩu mặc định. Manager/Receptionist vẫn bị giới hạn bởi assignment center và các rule nghiệp vụ ở service.

### Quyết định triển khai Flow 1

- Mỗi User có một role theo `User.RoleId`; mỗi MemberProfile và StaffProfile gắn tối đa một center. Member chưa chọn center được gắn vào center của gói mua đầu tiên trong transaction.
- MVP không bắt buộc email verification; tài khoản được kích hoạt sau đăng ký. Số điện thoại là tùy chọn. Mật khẩu tối thiểu 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt; khóa 15 phút sau 5 lần đăng nhập sai.
- Subscription bắt đầu sau khi invoice được thanh toán đủ. Khi còn một subscription Active chưa hết hạn, lần mua/gia hạn mới bắt đầu vào ngày kế tiếp ngày hết hạn đó; nếu không, bắt đầu vào ngày nhận đủ tiền.
- Ngày kết thúc là inclusive: `end_date = start_date + duration_days - 1`. `duration_days` được chụp trên subscription lúc mua và các ngày hiệu lực để trống khi PendingPayment.
- Token Bearer được bảo vệ bằng ASP.NET Core Data Protection, hết hạn sau 1 giờ; mỗi request đọc lại trạng thái tài khoản và role hiện tại.
- Lịch sử center cũ chỉ được gán tự động khi invoice/subscription xác định duy nhất một center; dữ liệu mơ hồ giữ nguyên không gán để tránh mở rộng sai phạm vi PII.
- `AccessType` hiện chỉ bị giới hạn độ dài; các giá trị enum hợp lệ cần được chốt trước khi siết validation.

## 3. Thuật ngữ và trạng thái đề xuất

- **User:** thông tin đăng nhập và trạng thái tài khoản.
- **MemberProfile:** hồ sơ nghiệp vụ của thành viên gắn với User.
- **MembershipPackage:** gói được trung tâm bán, gồm giá, thời hạn và quyền lợi.
- **MemberSubscription:** lần mua/gia hạn gói của một thành viên, có ngày hiệu lực và trạng thái riêng.
- **ClassEnrollment:** đăng ký tham gia một lớp theo kỳ/khóa.
- **ClassSession:** một buổi học cụ thể được tạo từ lịch lớp.
- **SessionBooking:** chỗ của thành viên trong một buổi cụ thể, nếu lớp cho phép đặt từng buổi.
- **Invoice / InvoiceItem / Payment:** chứng từ phải thu / dòng hàng / lần ghi nhận thanh toán.

Giá trị trạng thái phải được chuẩn hóa thành enum/constants hoặc bảng tra cứu và được validate ở backend. Danh sách dưới đây là đề xuất, không khẳng định schema hiện đã ép các giá trị này.

| Đối tượng | Trạng thái đề xuất |
|---|---|
| User | `PendingVerification`, `Active`, `Locked`, `Suspended`, `Disabled` |
| MembershipPackage | `Draft`, `Active`, `Inactive` |
| MemberSubscription | `PendingPayment`, `Active`, `Expired`, `Cancelled`, `Suspended`, `Refunded` |
| Class / ClassSchedule | `Draft`, `Published`, `Cancelled`, `Completed` |
| ClassEnrollment / SessionBooking | `Pending`, `Confirmed`, `Waitlisted`, `Cancelled`, `Attended`, `NoShow` |
| Invoice | `Draft`, `Issued`, `PartiallyPaid`, `Paid`, `Overpaid`, `Voided`, `PaidAfterVoid`, `PartiallyRefunded`, `Refunded` |
| Payment | `Pending`, `Succeeded`, `Failed`, `Voided`; trạng thái refund được quản lý riêng ở `PaymentRefund` |

## 4. Flow 1 — User and Membership Management

### 4.1 Mục tiêu

Tạo hoặc quản lý tài khoản và hồ sơ thành viên; chọn, mua hoặc gia hạn gói; chỉ kích hoạt quyền lợi thành viên khi đáp ứng chính sách thanh toán.

### 4.2 Luồng chính: Member tự đăng ký và mua gói

1. Member gửi thông tin đăng ký: họ tên, email, mật khẩu, số điện thoại tùy chọn và (nếu cần) center.
2. Hệ thống validate dữ liệu, chuẩn hóa email/số điện thoại và kiểm tra trùng.
3. Hệ thống tạo User Active với role Member (MVP không xác minh email); tạo MemberProfile và mã thành viên.
4. Member xem danh sách gói đang bán tại center; chọn một gói và gửi yêu cầu mua.
5. Hệ thống kiểm tra gói còn Active, thuộc center hợp lệ và điều kiện mua/gia hạn.
6. Hệ thống tạo MemberSubscription ở `PendingPayment`, chốt snapshot giá/thời hạn, tạo Invoice và InvoiceItem.
7. Flow 3 ghi nhận thanh toán. Khi thanh toán thành công, hệ thống kích hoạt subscription và thiết lập ngày bắt đầu/kết thúc. API trả trạng thái để frontend hiển thị; gửi email/push chưa thuộc phạm vi Flow 1 MVP.
8. Member xem hồ sơ, trạng thái gói và ngày hết hạn.

### 4.3 Luồng quầy: Receptionist đăng ký hoặc gia hạn

1. Receptionist tìm kiếm theo member code, email, số điện thoại hoặc tên.
2. Nếu chưa có hồ sơ, receptionist tạo User/MemberProfile với thông tin tối thiểu; hệ thống không được tạo tài khoản trùng.
3. Receptionist chọn gói hiện hành và thời điểm bắt đầu theo chính sách trung tâm.
4. Hệ thống tạo subscription chờ thanh toán, hóa đơn và dòng hóa đơn; `registered_by/created_by` ghi nhận nhân viên thực hiện.
5. Receptionist thu tiền ở Flow 3. Chỉ sau khi thanh toán thành công mới xác nhận quyền lợi gói.
6. Hệ thống lưu lịch sử thay đổi và trả mã hóa đơn/trạng thái để in hoặc xuất.

### 4.4 Happy cases

| ID | Tình huống | Kết quả mong đợi |
|---|---|---|
| F1-H1 | Member đăng ký bằng email/số điện thoại chưa tồn tại và dữ liệu hợp lệ. | Tạo đúng một User và MemberProfile; role Member; trả mã thành viên; không lộ password hash. |
| F1-H2 | Member chọn gói đang Active. | Tạo subscription `PendingPayment`, invoice và invoice item cùng transaction; giá/thời hạn được lưu snapshot. |
| F1-H3 | Receptionist tạo member tại quầy rồi thu đủ tiền. | User/profile/subscription/invoice/payment liên kết đúng; ghi người thao tác; gói Active sau khi payment thành công. |
| F1-H4 | Member gia hạn khi subscription hiện tại còn hiệu lực. | Sau khi thanh toán đủ, gói mới bắt đầu vào ngày kế tiếp ngày hết hạn hiện tại; ngày kết thúc inclusive; subscription cũ được giữ nguyên. |
| F1-H5 | Manager vô hiệu hóa một gói. | Gói không xuất hiện trong lựa chọn mua mới; các subscription đã mua và hóa đơn cũ vẫn giữ nguyên lịch sử. |
| F1-H6 | Member sửa hồ sơ, email hoặc số điện thoại; Manager cập nhật hồ sơ tại center. | Chuẩn hóa và kiểm tra trùng email/điện thoại; trường không gửi trong PATCH được giữ nguyên; chỉ ghi metadata thay đổi, không đưa PII vào audit. |
| F1-H7 | Manager/Receptionist tạo hoặc cập nhật Coach/Receptionist; Manager cập nhật trạng thái Member. | User và profile được lưu cùng transaction; status tài khoản được kiểm tra ở mọi request; audit có actor, center, thời điểm và thay đổi an toàn. |
| F1-H8 | System Admin thay ma trận quyền hoặc xem audit toàn hệ thống. | Chỉ role SystemAdmin truy cập được; thay đổi quyền và audit được lưu trong transaction. |
| F1-H9 | Client retry yêu cầu tạo subscription đang chờ hoặc thanh toán tiền mặt với cùng idempotency key. | Subscription/invoice đang mở được trả lại; cash payment không ghi hai lần nếu cùng actor/key/request. |
| F1-H10 | Giá hoặc thời hạn gói thay đổi sau khi invoice đã phát hành nhưng trước khi thanh toán. | Invoice, invoice item và subscription giữ snapshot đã chốt lúc mua; thay đổi danh mục không sửa ngược giao dịch đã tạo. |
| F1-H11 | Cổng thanh toán xác nhận thành công nhưng callback/reconcile được gửi lại cùng giao dịch. | Payment chỉ được ghi nhận một lần; invoice và subscription đạt trạng thái cuối nhất quán; lần xử lý lại trả kết quả idempotent. |

### 4.5 Unhappy cases / ngoại lệ

| ID | Tình huống | Xử lý mong đợi |
|---|---|---|
| F1-U1 | Email/username đã được dùng. | Từ chối tạo mới với lỗi validation có thể hiểu; không tạo User/profile dở dang. |
| F1-U2 | Thiếu trường bắt buộc, email sai định dạng, mật khẩu không đạt chính sách hoặc số điện thoại sai. | Trả lỗi theo trường; không ghi dữ liệu. |
| F1-U3 | Gói không tồn tại, hết hạn bán, Inactive hoặc thuộc center khác. | Từ chối mua; không tạo subscription/invoice. |
| F1-U4 | Thanh toán thất bại hoặc người dùng hủy ở cổng thanh toán. | Giữ invoice/subscription ở trạng thái chờ/thất bại có thể truy vết; không kích hoạt membership; cho phép retry theo quy tắc. |
| F1-U5 | Receptionist không có quyền hoặc thao tác ở center khác. | Trả 403; ghi audit event bảo mật nếu phù hợp. |
| F1-U6 | Hai request mua/gia hạn được gửi đồng thời hoặc client retry. | Không tạo giao dịch trùng ngoài ý muốn; hỗ trợ idempotency và transaction. |
| F1-U7 | Database lỗi giữa tạo hồ sơ, subscription và invoice. | Rollback toàn bộ bước tạo liên quan; không để hồ sơ hoặc hóa đơn mồ côi. |
| F1-U8 | Tài khoản bị khóa/disabled cố mua gói. | Từ chối theo trạng thái tài khoản; không thay đổi dữ liệu. |
| F1-U9 | PATCH không gửi email/điện thoại, hoặc gửi email/điện thoại trùng user khác. | Trường bị bỏ qua được giữ nguyên; dữ liệu trùng bị từ chối và transaction rollback. |
| F1-U10 | Manager/Receptionist truy cập sai center, sửa role matrix hoặc tự tạo System Admin. | Trả 403; không thay đổi dữ liệu. Chỉ System Admin bootstrap/đổi ma trận theo chính sách. |
| F1-U11 | System Admin cuối cùng cố gỡ quyền quản lý ma trận; gán quyền này cho role khác; hoặc gửi permission code không tồn tại. | Từ chối toàn bộ thay đổi; ma trận hợp lệ trước đó vẫn còn. |
| F1-U12 | VNPay callback thất bại, lặp, sai tiền tệ, trùng mã giao dịch hoặc báo thành công với invoice đã hủy/đã trả/amount vượt dư nợ. | Callback đã ký được lưu để truy vết. Trường hợp cổng báo thành công nhưng không thể đối soát vào invoice được đánh `ReviewRequired`; không kích hoạt membership và không cộng doanh thu tự động. |
| F1-U13 | Hai nhân viên ghi cùng cash payment; idempotency key được tái dùng với invoice/amount khác. | Cùng key và cùng request trả payment đã ghi; khác request nhận conflict; không thu vượt dư nợ. |
| F1-U14 | Hệ thống lỗi khi lưu user/profile, invoice/payment, quyền hoặc audit. | Các thay đổi trong cùng transaction rollback; không để entity nghiệp vụ thành công nhưng thiếu audit tương ứng. |
| F1-U15 | Đăng ký với email/điện thoại trùng sau khi chuẩn hóa hoa-thường, khoảng trắng hoặc định dạng. | Ràng buộc uniqueness vẫn chặn tạo bản ghi trùng; không để hai tài khoản đại diện cùng một định danh. |
| F1-U16 | Member đọc hoặc sửa hồ sơ/subscription của Member khác bằng cách đổi ID trên request. | API từ chối (`403`/`404` theo chính sách); không trả PII, lịch sử gói hoặc hóa đơn của người khác. |
| F1-U17 | Tài khoản đăng nhập sai đến ngưỡng khóa; thử mật khẩu đúng trong và sau thời gian khóa. | Khóa 15 phút sau 5 lần sai; mật khẩu đúng trong thời gian khóa vẫn bị từ chối; sau 15 phút đăng nhập được nếu không có trạng thái chặn khác. |
| F1-U18 | Hai yêu cầu mua/gia hạn đồng thời cho cùng Member và cùng gói. | Không tạo hai khoản thu hoặc hai entitlement trùng ngoài ý muốn; request thứ hai trả kết quả hiện có hoặc conflict theo chính sách idempotency. |
| F1-U19 | Provider gửi callback sai chữ ký. | Không thay đổi Payment/Invoice/Subscription; ghi log an toàn, không ghi secret hoặc dữ liệu nhạy cảm của callback. |
| F1-U20 | Callback có amount, currency hoặc gateway reference không khớp invoice/payment intent. | Mỗi điều kiện không khớp đều bị từ chối/đưa vào `ReviewRequired`; không kích hoạt subscription và không cộng doanh thu. |
| F1-U21 | Provider báo đã thu tiền nhưng database lỗi trước khi hoàn tất cập nhật invoice/subscription. | Giao dịch có thể được reconcile an toàn; retry không tạo payment hoặc kích hoạt subscription lần hai; không mất dấu khoản tiền provider đã thu. |
| F1-U22 | Subscription được truy vấn đúng tại ngày hết hạn và ngay sau ngày hết hạn theo timezone center. | Ngày kết thúc inclusive được áp dụng nhất quán; quyền lợi hết đúng thời điểm theo ngày nghiệp vụ, không lệch do chuyển UTC/local time. |

### 4.6 Acceptance criteria chính

- **AC-F1-01:** Given email đã tồn tại, when đăng ký, then API trả conflict/validation error và không tạo bản ghi mới.
- **AC-F1-02:** Given gói không Active, when member/receptionist mua, then hệ thống từ chối và không tạo invoice/subscription.
- **AC-F1-03:** Given invoice chưa thanh toán đủ, when truy vấn quyền lợi gói, then subscription không được coi là Active.
- **AC-F1-04:** Given thanh toán thành công, when xử lý hoàn tất, then subscription và invoice được cập nhật nhất quán, có ngày hiệu lực và ngày hết hạn.
- **AC-F1-05:** Given receptionist tạo/gia hạn gói, then actor và thời điểm thao tác được lưu để audit.
- **AC-F1-06:** Given một request đăng ký hoặc cash payment được retry, then hệ thống không tạo thêm subscription/payment cho cùng request.
- **AC-F1-07:** Given tài khoản không có permission hoặc không thuộc center, then API trả 403/404 phù hợp và không rò dữ liệu ngoài phạm vi.
- **AC-F1-08:** Given callback VNPay đã xác minh chữ ký nhưng giao dịch không thể gắn vào invoice hợp lệ, then attempt/audit được lưu; chỉ payment `Succeeded` mới được tính doanh thu hoặc kích hoạt subscription.
- **AC-F1-09:** Given yêu cầu thay ma trận quyền từ role khác SystemAdmin hoặc yêu cầu gỡ quyền cuối của SystemAdmin, then hệ thống từ chối và giữ ma trận cũ.
- **AC-F1-10:** Given cùng gateway transaction được xử lý nhiều lần hoặc lỗi xảy ra sau khi provider đã thu tiền, then reconciliation không tạo payment/entitlement trùng và không bỏ mất khoản thu.
- **AC-F1-11:** Given request truy cập dữ liệu Member khác, tài khoản đang bị khóa hoặc subscription ở ranh giới hết hạn, then authorization và ngày hiệu lực được kiểm tra ở backend; không lộ dữ liệu ngoài scope.

## 5. Flow 2 — Class Booking and Schedule Management

### API contract đã triển khai (MVP)

- `GET /api/centers/{centerId}/sports` và `/rooms`: lựa chọn bộ môn/phòng đang hoạt động.
- Manager: `POST /api/classes`, `POST /api/classes/{id}/schedules`, `POST /api/classes/{id}/coaches`, `POST /api/classes/{id}/publish`.
- Danh mục lớp Published: `GET /api/centers/{centerId}/classes`; thời khóa biểu theo ngày: `GET /api/centers/{centerId}/schedule?from=YYYY-MM-DD&to=YYYY-MM-DD`; lịch buổi theo lớp: `GET /api/classes/{id}/sessions?from=&to=`.
- Member: `POST /api/classes/{id}/enrollments` với `{ "subscriptionId": 123 }`, `GET /api/members/me/enrollments`, và `DELETE /api/classes/{id}/enrollments/{enrollmentId}` với body tùy chọn `{ "reason": "..." }`.
- JWT quyết định Member hiện tại; client không được chọn `memberId`. Ghi danh yêu cầu subscription đang Active/còn hạn/đúng cơ sở, đủ quota, lớp Published và chỗ còn trống. Sức chứa được kiểm tra trong transaction Serializable.
- Hủy ghi danh cho phép đến 2 giờ trước buổi Scheduled tiếp theo theo giờ Việt Nam (UTC+7), sau đó trả `409 Conflict`. Policy này được dùng vì đặc tả trước đó chưa quy định mốc cụ thể.
- Lịch lặp tạo `ClassSession` cho từng ngày phù hợp; ngày trong tuần theo .NET (`0` = Chủ nhật). API lịch công khai lọc buổi qua `from`/`to`.

### 5.1 Mục tiêu

Manager tạo lớp, phòng, lịch và phân công coach; member ghi danh lớp hoặc đặt buổi; hệ thống bảo vệ sức chứa, quyền tham gia và tránh xung đột lịch.

### 5.2 Luồng setup của Manager

1. Manager tạo/cập nhật lớp với center, bộ môn, tên, sức chứa, thời lượng/trạng thái.
2. Manager tạo lịch lặp (ngày trong tuần, giờ bắt đầu/kết thúc, ngày hiệu lực) và phòng.
3. Manager phân công một hoặc nhiều coach đủ điều kiện.
4. Hệ thống kiểm tra phòng và coach không bị trùng giờ trong cùng center; lớp chỉ được Publish khi dữ liệu bắt buộc hợp lệ.
5. Hệ thống sinh ClassSession cho các ngày nằm trong khoảng lịch được tạo; session giữ snapshot coach/phòng/thời gian để lịch sử không đổi khi template tương lai được sửa.
6. Member xem lớp/session đã Published, sức chứa và điều kiện tham gia.

### 5.3 Luồng đăng ký lớp/buổi của Member hoặc Receptionist

1. Member chọn lớp; hệ thống xác thực user đang Active và member profile hợp lệ.
2. Hệ thống kiểm tra subscription có quyền tham gia, còn hiệu lực, đúng center/access type và còn quota nếu gói giới hạn số lớp.
3. Hệ thống kiểm tra lớp/session Published, chưa đóng đăng ký, còn chỗ, không trùng giờ với booking khác và member chưa đăng ký.
4. Nếu nghiệp vụ là đăng ký cả khóa, tạo ClassEnrollment. Nếu nghiệp vụ là đặt buổi riêng lẻ, tạo SessionBooking. Không tạo cả hai kiểu cho cùng thao tác nếu chưa định nghĩa quan hệ.
5. Ghi nhận đăng ký, thời điểm và actor (`Member` hoặc `Receptionist`); trả xác nhận hoặc vị trí waitlist.
6. Khi hủy theo chính sách, chuyển trạng thái và giải phóng chỗ; nếu có waitlist, mời người tiếp theo theo quy tắc đã chốt.

### 5.4 Happy cases

| ID | Tình huống | Kết quả mong đợi |
|---|---|---|
| F2-H1 | Manager tạo lớp, lịch, phòng và gán coach không bị trùng. | Dữ liệu được lưu; sessions được sinh trong khoảng hiệu lực; lịch hiển thị chính xác. |
| F2-H2 | Member có subscription đủ điều kiện đăng ký lớp còn chỗ. | Tạo enrollment/booking duy nhất; cập nhật số chỗ chính xác; gửi xác nhận. |
| F2-H3 | Receptionist ghi danh member tại quầy. | Kiểm tra cùng rule như member tự đăng ký; lưu `registered_by`; member thấy lớp trong lịch cá nhân. |
| F2-H4 | Lớp đầy nhưng bật waitlist. | Đăng ký được lưu trạng thái `Waitlisted`, không tính là chỗ đã xác nhận. |
| F2-H5 | Coach xem lịch. | Chỉ thấy sessions thuộc lớp được phân công; member roster chỉ gồm người đã xác nhận. |
| F2-H6 | Member hủy đúng thời hạn. | Booking/enrollment chuyển `Cancelled`; giữ lịch sử và giải phóng chỗ đúng một lần. |
| F2-H7 | Member hủy đúng hạn khi có waitlist. | Chỗ được giải phóng và chỉ Member đầu tiên đủ điều kiện được promote một lần; lịch cá nhân và trạng thái waitlist được cập nhật nhất quán. |

### 5.5 Unhappy cases / ngoại lệ

| ID | Tình huống | Xử lý mong đợi |
|---|---|---|
| F2-U1 | Gói hết hạn, chưa thanh toán hoặc không có quyền vào lớp. | Từ chối đăng ký; trả lý do nghiệp vụ; không giữ chỗ. |
| F2-U2 | Lớp/session hủy, chưa Published, đóng đăng ký hoặc đã bắt đầu. | Từ chối booking; trả trạng thái hiện tại. |
| F2-U3 | Lớp đầy và không bật waitlist. | Từ chối với lỗi capacity; không tạo booking xác nhận. |
| F2-U4 | Hai người cùng đặt chỗ cuối cùng. | Chỉ một request được xác nhận; request còn lại nhận full/waitlist; kiểm chứng trên relational database với request chạy đồng thời, không chỉ bằng EF InMemory. |
| F2-U5 | Member đã đăng ký cùng lớp/session hoặc gửi lại request. | Không tạo duplicate; trả booking hiện tại hoặc lỗi conflict theo API contract. |
| F2-U6 | Coach hoặc phòng bị trùng lịch. | Không cho Publish/lưu lịch bị xung đột; trả đối tượng và khung giờ xung đột. |
| F2-U7 | Coach không được phân công gọi API lịch/roster lớp. | Trả 403/404 theo chính sách; không lộ roster ngoài phạm vi. |
| F2-U8 | Member hủy sau deadline hoặc session đã diễn ra. | Từ chối hủy tự phục vụ hoặc chuyển sang yêu cầu hỗ trợ; giữ nguyên attendance và lịch sử. |
| F2-U9 | Đổi lịch sau khi đã có booking. | Cập nhật session theo chính sách; thông báo người bị ảnh hưởng; không âm thầm chuyển booking sang lịch khác. |
| F2-U10 | Cùng Member gửi hai request đặt cùng session đồng thời hoặc retry sau timeout. | Chỉ có một booking active; retry cùng request trả kết quả idempotent; không giữ hai chỗ hoặc tạo hai waitlist entry. |
| F2-U11 | Hai cancellation đồng thời làm trống chỗ trong khi có nhiều người waitlist. | Một chỗ chỉ promote một người; thứ tự và tính đủ điều kiện tuân theo chính sách waitlist đã chốt; không promote lặp hoặc vượt capacity. |
| F2-U12 | Người được promote đã có booking khác trùng giờ, đã hủy tài khoản hoặc không còn subscription hợp lệ. | Hệ thống không tạo booking không hợp lệ; xử lý tiếp người kế tiếp hoặc giữ chỗ theo chính sách waitlist được chốt và ghi lại kết quả. |
| F2-U13 | Member dùng subscription sai center, sai access type, hết quota hoặc hết hạn tại thời điểm session bắt đầu. | Từ chối booking trước khi giữ chỗ; response không tiết lộ dữ liệu lớp ngoài center. |
| F2-U14 | Manager hủy lớp sau khi đã có booking. | Không xóa lịch sử; session và booking được chuyển trạng thái nhất quán theo policy; danh sách Member bị ảnh hưởng có thể truy vết và nhận thông báo theo phạm vi hỗ trợ. |
| F2-U15 | Coach gọi API xem roster của lớp không được phân công hoặc Member đổi ID để xem booking của người khác. | Trả `403`/`404`; không trả roster, mục tiêu tập luyện hoặc dữ liệu cá nhân ngoài phạm vi. |
| F2-U16 | Đặt/hủy ở đúng ranh giới mở/đóng booking và mốc hủy trước buổi tập 2 giờ. | Kết quả nhất quán theo timezone `Asia/Ho_Chi_Minh`; kiểm thử trước, đúng tại và sau mốc, không phụ thuộc timezone máy chủ. |

### 5.6 Acceptance criteria chính

- **AC-F2-01:** Given member không có subscription hợp lệ, when đăng ký lớp yêu cầu gói, then booking bị từ chối và không chiếm chỗ.
- **AC-F2-02:** Given session còn một chỗ và có hai request đồng thời, then tối đa một booking được xác nhận.
- **AC-F2-03:** Given member đã booking một session, when gửi lại cùng yêu cầu, then không phát sinh booking thứ hai.
- **AC-F2-04:** Given coach/phòng có session trùng thời gian, when manager Publish lịch mới, then hệ thống từ chối và nêu xung đột.
- **AC-F2-05:** Given coach được gán vào lớp, when xem lịch, then chỉ thấy lớp/session được phép truy cập.
- **AC-F2-06:** Given booking bị hủy hợp lệ, then chỗ được giải phóng đúng một lần và trạng thái cũ được lưu.
- **AC-F2-07:** Given concurrent booking/cancel/waitlist promotion, then sức chứa không vượt giới hạn và mỗi chỗ chỉ được cấp cho một Member.
- **AC-F2-08:** Given Manager đổi lịch hoặc Coach/Member truy cập ngoài scope, then lịch sử booking được bảo toàn và dữ liệu không được phép không bị trả về.

## 6. Flow 3 — Payment and Report Management

### 6.1 Mục tiêu

Tạo khoản phải thu có chi tiết và giá tại thời điểm bán; ghi nhận một hoặc nhiều lần thanh toán; phát hành hóa đơn/biên nhận; tổng hợp báo cáo doanh thu từ giao dịch đã thành công.

### 6.2 Luồng thanh toán

1. Flow 1/2 tạo yêu cầu tính phí (ví dụ bán gói; chỉ thu phí lớp nếu chính sách xác nhận) và gửi danh sách sản phẩm/dịch vụ.
2. Backend lấy giá hiện hành từ DB, không tin giá client gửi lên. Discount/tax hiện chưa có chính sách hoặc API cấu hình nên checkout đang lưu `discount=0`, `tax=0`.
3. Tạo Invoice và InvoiceItem với snapshot mô tả, quantity, unit price, amount, subtotal/discount/tax/total; trạng thái `Issued`.
4. Member thanh toán trực tuyến hoặc receptionist ghi nhận tiền mặt/phương thức được cho phép.
5. Với cổng thanh toán, callback phải xác minh chữ ký/trạng thái và idempotency; với tiền mặt, lưu người thu và thời gian.
6. Lưu Payment. Cập nhật tổng đã trả và Invoice thành `Paid` hoặc `PartiallyPaid`.
7. Khi đủ tiền, kích hoạt subscription liên quan; phát hành mã/số hóa đơn và cho phép tra cứu để xem/in/xuất ở frontend.
8. Manager lọc báo cáo theo center và khoảng thời gian; doanh thu lấy từ payment thành công theo ngày ghi nhận, trừ refund/void theo quy tắc báo cáo.

### 6.3 Happy cases

| ID | Tình huống | Kết quả mong đợi |
|---|---|---|
| F3-H1 | Member thanh toán đủ hóa đơn mua gói. | Payment `Succeeded`, Invoice `Paid`, subscription được Active; số tiền khớp total. |
| F3-H2 | Receptionist thu tiền mặt. | Payment có method/cashier/time/amount; invoice cập nhật; receipt có thể tra cứu/in. |
| F3-H3 | Hóa đơn được trả nhiều lần. | Các payment được lưu riêng; invoice `PartiallyPaid` cho đến khi tổng thành công đạt total. |
| F3-H4 | Manager xem doanh thu theo ngày/tháng/center. | Tổng hợp chỉ lấy giao dịch thành công, lọc đúng múi giờ và center, có thể đối soát về invoice/payment. |
| F3-H5 | Khách yêu cầu xuất lại hóa đơn đã thanh toán. | Trả đúng chứng từ đã lưu, không tạo khoản thu mới. |
| F3-H6 | Invoice được thanh toán nhiều lần và lần cuối vừa đủ số dư. | Trạng thái đi từ `PartiallyPaid` sang `Paid` đúng một lần; entitlement chỉ kích hoạt một lần; tổng payment bằng total invoice. |
| F3-H7 | Refund toàn phần hoặc một phần được provider xác nhận và reconcile thành công. | Lưu refund riêng, giữ payment gốc, lưu người duyệt; receipt và báo cáo thể hiện khoản refund theo policy đã chốt. |

### 6.4 Unhappy cases / ngoại lệ

| ID | Tình huống | Xử lý mong đợi |
|---|---|---|
| F3-U1 | Thanh toán thất bại/hủy/timeout. | Lưu trạng thái thất bại hoặc pending để tra soát; không kích hoạt quyền lợi; cho phép retry không nhân đôi khoản thu. |
| F3-U2 | Callback gửi lặp hoặc client retry request. | Xử lý idempotent; chỉ ghi nhận một payment thành công cho transaction ngoài. |
| F3-U3 | Callback amount/currency/invoice không khớp hoặc chữ ký sai. | Từ chối cập nhật; ghi log an toàn; không đánh dấu invoice Paid. |
| F3-U4 | Khách đưa tiền mặt nhiều hơn số dư hóa đơn. | Payment chỉ ghi nhận số tiền đến hạn; tiền thừa được tính thành tiền trả lại, không làm invoice thành `Overpaid`. |
| F3-U5 | Invoice bị void/đã Paid nhưng nhận thêm lệnh thu; callback thành công đến muộn sau khi void. | Từ chối lệnh thu mới. Nếu cổng xác nhận đã thu tiền sau void, lưu payment thành công, đặt invoice `PaidAfterVoid` và yêu cầu Manager/Admin refund; không bỏ qua khoản tiền đã thu. |
| F3-U6 | Yêu cầu refund vượt số tiền đã thu hoặc do người không có quyền duyệt gửi. | Từ chối; không tạo refund thành công hoặc sửa payment/invoice gốc; ghi actor và lý do từ chối an toàn. |
| F3-U7 | Giao dịch chưa rõ trạng thái do cổng thanh toán timeout. | Giữ pending và đối soát; không giả định thất bại hoặc thành công chỉ từ timeout. |
| F3-U8 | Nhân viên không có quyền xem báo cáo hoặc ghi payment ở center khác. | Từ chối truy cập (`403`; center ngoài scope có thể trả `404` để không tiết lộ dữ liệu), không trả nội dung tài chính. |
| F3-U9 | Hai request thu tiền đồng thời, trùng idempotency key hoặc deadlock DB. | Ràng buộc unique/transaction ngăn ghi trùng; cùng key/cùng payload idempotent, cùng key/khác payload trả `409`; deadlock trả lỗi retryable và không tạo khoản thu thứ hai. |
| F3-U10 | Callback/IPN có chữ ký sai hoặc thiếu trường chữ ký bắt buộc. | Không thay đổi dữ liệu tài chính; ghi log an toàn, không lưu secret hoặc toàn bộ payload nhạy cảm. |
| F3-U11 | Callback có amount khác số tiền intent/invoice hoặc vượt số dư còn phải thu. | Không tự đánh dấu `Paid`; khoản provider đã xác nhận thu nhưng không đối soát được chuyển `ReviewRequired`/`Overpaid` theo policy và không được cộng nhầm doanh thu. |
| F3-U12 | Callback có currency, invoice number, gateway reference hoặc transaction code không khớp. | Không gắn payment vào invoice khác; giữ bằng chứng đối soát; không kích hoạt subscription. Mỗi kiểu mismatch có test riêng. |
| F3-U13 | Cùng gateway transaction gửi callback lặp; lần lặp có payload mâu thuẫn với lần đầu. | Callback giống hệt được xử lý idempotent; payload mâu thuẫn không ghi đè trạng thái thành công và được đưa vào đối soát. |
| F3-U14 | Provider timeout nhưng sau đó gửi callback thành công hoặc Manager reconcile giao dịch. | Pending được hoàn tất đúng một lần; không ghi nhận thất bại vĩnh viễn, không cộng doanh thu hai lần. |
| F3-U15 | Thu tiền mặt khi tender lớn hơn dư nợ; thu tiếp khi invoice đã Paid; hoặc hai cashier cùng thu số dư cuối. | Tiền thối tách khỏi amount đã thanh toán; không over-collect ngoài policy; chỉ một request dùng được số dư cuối. |
| F3-U16 | Refund callback/reconcile gửi lặp, refund một phần rồi refund tiếp vượt số tiền đã thu, hoặc refund thất bại. | Không refund vượt khoản thu ròng; giữ payment gốc và lịch sử các lần refund; số liệu gross/refund/net không bị nhân đôi. |
| F3-U17 | Báo cáo chạy sát ranh giới ngày/tháng, có payment pending/failed/void/refund hoặc center không thuộc quyền nhân viên. | Tính theo timezone center và policy ghi nhận doanh thu; pending/failed không tính; refund được phân kỳ nhất quán; scope sai không trả dữ liệu tài chính. |
| F3-U18 | Invoice/payment đồng thời được cập nhật từ cash checkout và callback online. | Transaction/idempotency ngăn tổng thu vượt dư nợ hoặc trạng thái bị ghi đè; mọi giao dịch provider đã thu đều có đường reconcile. |

### 6.5 Acceptance criteria chính

- **AC-F3-01:** Given client gửi unit price khác giá trong DB, when tạo invoice, then backend dùng giá DB hoặc từ chối; không dùng giá client.
- **AC-F3-02:** Given tổng payment thành công bằng total invoice, then invoice chuyển Paid đúng một lần và entitlement liên quan được kích hoạt.
- **AC-F3-03:** Given callback thành công gửi lại nhiều lần với cùng transaction code, then chỉ có một giao dịch được tính doanh thu.
- **AC-F3-04:** Given payment thất bại hoặc pending, then báo cáo doanh thu không cộng khoản đó.
- **AC-F3-05:** Given refund, then payment gốc không bị xóa và báo cáo thể hiện doanh thu gộp/refund/ròng theo kỳ.
- **AC-F3-06:** Given khoảng ngày báo cáo [from, to], then điều kiện ngày áp dụng nhất quán theo timezone báo cáo được cấu hình và có thể đối soát với danh sách giao dịch.
- **AC-F3-07:** Given provider callback thiếu/sai chữ ký hoặc sai amount/currency/reference, then không payment nào được gắn nhầm invoice hoặc kích hoạt entitlement.
- **AC-F3-08:** Given webhook, reconcile, cash checkout hoặc refund được retry đồng thời, then mỗi giao dịch chỉ được hạch toán một lần và tổng thu/refund không vượt giới hạn hợp lệ.
- **AC-F3-09:** Given payment, void, refund và báo cáo thuộc các kỳ khác nhau, then gross/refund/net có thể đối soát về giao dịch nguồn theo một policy thời gian được cấu hình.

### 6.6 Hợp đồng Flow 3 đã triển khai trong backend

```text
POST /api/auth/login
POST /api/payments/create-vnpay-url                 # Member + Idempotency-Key
POST /api/payments/create-momo-url                  # Member + Idempotency-Key
GET  /api/payments/vnpay-callback                   # public, chữ ký VNPay
GET  /api/payments/vnpay-ipn                        # public, xác nhận kết quả cho VNPay
GET  /api/payments/momo-callback                    # public, chữ ký MoMo
POST /api/payments/momo-ipn                         # public, chữ ký MoMo
POST /api/payments/counter-checkout                 # Receptionist/Manager/Admin
POST /api/invoices/{invoiceNumber}/payments         # thu tiếp hóa đơn tại quầy
GET  /api/invoices/{invoiceNumber}                  # kiểm tra quyền sở hữu/center
POST /api/payments/counter-void/{invoiceNumber}     # Manager/Admin; chỉ hóa đơn chưa thu
POST /api/payments/{paymentId}/refunds              # Manager/Admin
POST /api/payments/refunds/{refundId}/reconcile     # Manager/Admin; refund Pending
POST /api/payments/{gatewayReference}/reconcile     # Manager/Admin; payment Pending
GET  /api/reports/revenue?centerId=&from=&to=&groupBy=day|month
GET  /api/reports/membership?centerId=&from=&to=&groupBy=day|month
GET  /api/reports/classes?centerId=&from=&to=&groupBy=day|month
```

Các route thanh toán cần JWT và `Idempotency-Key`; callback/IPN là ngoại lệ public nhưng chỉ áp dụng dữ liệu sau khi xác minh chữ ký, reference, amount và currency phù hợp. Cash được thu một phần; POS phải thu đúng số dư. Timeout giữ `Pending` để đối soát; không cho tạo lần thử mới khi còn payment Pending. Manager chỉ có scope center trong JWT; Admin phải chỉ định `centerId` khi xem report.

Refund được ghi ở bảng `PaymentRefund`, giữ nguyên payment gốc. Cash/POS cần thao tác xác nhận refund bên ngoài; MoMo/VNPay gọi API refund. Timeout/response không xác thực giữ refund Pending và có endpoint reconcile. Báo cáo gộp lấy payment Succeeded theo `PaidAt`; refund trừ theo `ProcessedAt` trong kỳ refund.

Schema hiện không lưu `CenterId` trên `MemberProfile`. Báo cáo membership quy một member về center nếu hồ sơ có ít nhất một subscription tại center đó; `NewMembers` được tính từ subscription đầu tiên của hồ sơ tại center. Số liệu không bao gồm hồ sơ chưa từng có subscription ở center.

## 7. Đánh giá DB hiện có và đề xuất

### 7.1 Đã có nền tảng phù hợp

Trong `SportsCenterManagement.Models/SportsCenterEntities.cs` và `SportsCenterManagement.Services/SportsCenterDbContext.cs` hiện có entities/DbSets cho:

- Tài khoản/quyền: `User`, `Role`, `Permission`, `RolePermission`, `MemberProfile`, `CoachProfile`, `StaffProfile`, `AuditLog`.
- Gói: `MembershipPackage`, `MemberSubscription`.
- Lớp/lịch: `Center`, `Sport`, `Room`, `ClassEntity`, `ClassCoach`, `ClassSchedule`, `ClassSession`, `ClassEnrollment`, `SessionBooking`, `ClassWaitlist`, `Attendance`.
- Thu tiền: `Invoice`, `InvoiceItem`, `Payment`, `PaymentRefund`.

Có unique index cho username/email và một số mã; có FK cấu hình bằng EF Core. Do đó không nên tạo lại các bảng này trước khi kiểm tra đầy đủ cột, unique constraint, check constraint, quan hệ và cách dùng trạng thái.

### 7.2 Cần rà soát/bổ sung trước khi chốt migrations

1. **Enrollment vs session booking:** Chốt đăng ký là ghi danh cả lớp hay đặt từng buổi. Nếu hỗ trợ cả hai, quy định một nguồn sự thật và tránh đếm capacity hai lần. Xác định `ClassEnrollment` là roster khóa học, còn `SessionBooking` là đặt từng session.
2. **Ràng buộc chống trùng:** Bổ sung unique `(ClassId, MemberId)` phù hợp cho enrollment lịch sử (cân nhắc filtered unique theo trạng thái nếu cho phép hủy rồi đăng ký lại); unique `(SessionId, MemberId)` cho booking; unique `(SessionId, MemberId)` cho Attendance. Tránh unique đơn giản nếu cần lưu nhiều lần hủy/đăng ký; cân nhắc bảng lịch sử riêng hoặc tái sử dụng bản ghi theo policy.
3. **Capacity concurrency:** Thêm `rowversion` hoặc cập nhật có điều kiện/transaction phù hợp để hai request không chiếm cùng chỗ cuối. Nên có unique/constraint và xử lý concurrency trong service.
4. **Recurring schedule:** Kiểm tra ClassSchedule có day-of-week, effective start/end, timezone, capacity và cờ waitlist hay không. Nếu thiếu, bổ sung; ClassSession phải chứa ngày/giờ cụ thể và snapshot room/coach/status.
5. **Coach assignment:** `ClassCoach` hiện là quan hệ nhiều-nhiều; cần hiệu lực từ/đến hoặc lịch sử assignment nếu coach thay đổi. Phải validate xung đột theo từng session, không chỉ theo lớp.
6. **Membership snapshot:** `MemberSubscription` cần lưu giá thực trả, ngày bắt đầu/kết thúc, người bán, trạng thái, invoice liên quan và quy tắc gia hạn. Package price/duration có thể đổi; lịch sử subscription không được đổi theo giá package hiện tại.
7. **Invoice snapshot:** `InvoiceItem` đang có mô tả/quantity/unit price/amount. Đảm bảo tính tiền decimal chính xác, quy tắc discount/tax, currency, issued/due time và không phụ thuộc giá hiện tại của package.
8. **Payment/refund:** Flow 3 đã bổ sung `Payment.CreatedAt`, nullable `PaidAt`, gateway/idempotency references và bảng `PaymentRefund`; giữ unique provider transaction và refund idempotency trong các migration sau.
9. **Quan hệ actor:** `Payment.ProcessedBy` và `Invoice.CreatedBy` cần FK tới User; xác thực actor đúng center/role. Làm rõ `Payment.MemberId` có thừa vì suy ra từ Invoice hay cần giữ snapshot; nếu giữ, kiểm tra member nhất quán với invoice.
10. **Báo cáo/audit:** `AuditLog` cần actor, action, entity type/id, timestamp UTC, correlation/request id và metadata đã lọc PII. Báo cáo doanh thu nên query từ payment/refund có trạng thái và thời điểm chuẩn hóa, không chỉ dựa `Invoice.PaidAt`.
11. **Multi-role:** `User.RoleId` chỉ biểu diễn một role tại một thời điểm. Nếu một user có thể vừa là coach vừa là manager/receptionist, đổi sang `UserRoles(UserId, RoleId, CenterId, ...)`; nếu mỗi tài khoản chỉ có đúng một role thì giữ thiết kế hiện tại.
12. **Constraints và indexes:** Thêm check constraints cho `price >= 0`, `duration_days > 0`, `end_time > start_time`, số lượng/sức chứa dương, amount không âm; index cho lọc theo center/date/status/member/session/invoice theo workload.

**Không khuyến nghị** tạo thêm `Transactions` nếu `Payment` đã là giao dịch thu tiền chính. Chỉ thêm bảng riêng khi phải lưu nhiều lần thử, callback, refund hoặc event đối soát độc lập.

## 8. API tham khảo cho các flow còn lại

Tên route là đề xuất để làm hợp đồng API; cần khớp convention nhóm trước khi triển khai.

```text
POST   /api/auth/register
GET    /api/members/{id}
PATCH  /api/members/{id}
GET    /api/membership-packages?centerId=&status=Active
POST   /api/members/{id}/subscriptions
GET    /api/members/{id}/subscriptions

POST   /api/classes                       # Manager
POST   /api/classes/{id}/schedules        # Manager
POST   /api/classes/{id}/coaches           # Manager
GET    /api/classes?centerId=&from=&to=
POST   /api/classes/{id}/enrollments       # Member/Receptionist
DELETE /api/classes/{id}/enrollments/{enrollmentId}
POST   /api/sessions/{id}/bookings
DELETE /api/sessions/{id}/bookings/{bookingId}
GET    /api/coaches/me/schedule            # Coach

POST   /api/invoices                       # nội bộ từ nghiệp vụ, không nhận total tùy ý từ client
```

Quy tắc: dùng DTO thay vì trả EF entity trực tiếp; API mutation có validation, authorization và audit. Không đưa endpoint thu tiền nội bộ ra public nếu chỉ callback hoặc service được phép gọi.

## 9. Giả định và câu hỏi mở cần xác nhận

### Assumptions (tạm dùng để viết flow)

- Mỗi dữ liệu nghiệp vụ thuộc một center; member/nhân viên được giới hạn phạm vi center.
- Giá và quyền lợi gói phải được snapshot tại thời điểm mua.
- Gói chỉ có hiệu lực sau khi thu đủ tiền; ngoại lệ miễn phí/ghi nợ cần policy riêng.
- Hóa đơn và payment là lịch sử tài chính, không xóa cứng sau khi phát hành.
- Mọi thời gian lưu trữ theo UTC; hiển thị theo timezone center.

### Open Questions (ưu tiên cần chốt)

1. Một user có thể có nhiều role hoặc làm ở nhiều center không?
2. Member tự đăng ký có cần xác minh email/OTP trước khi dùng hệ thống không?
3. Gói có giới hạn số buổi/lớp hay chỉ theo thời hạn? Quy tắc ghép gói với bộ môn/lớp nào?
4. MVP hiện đặt theo từng session và dùng mốc hủy tự phục vụ trước 2 giờ. Cần chốt đăng ký cả khóa, thứ tự/expiry của waitlist, xử lý người được promote không còn đủ điều kiện, no-show/phí hủy và đổi lịch sau khi đã có booking.
5. MVP hiện có tiền mặt, VNPay, MoMo, thanh toán một phần và reconcile refund; discount/tax đang bằng 0. Cần quyết định miễn phí/ghi nợ, discount và thuế trước khi hỗ trợ các trường hợp đó.
6. Báo cáo ghi nhận doanh thu theo ngày thu tiền hay ngày phát hành invoice? Refund ghi vào kỳ refund hay điều chỉnh kỳ payment gốc; `PaidAfterVoid`/`Overpaid` ảnh hưởng gross/net ra sao?
7. Center Manager và Receptionist được xem PII và dữ liệu tài chính trong phạm vi nào; cần che những trường nào trên màn hình, API và audit?

## 10. Ưu tiên triển khai

1. Chốt Open Questions 1–7; rà soát entity và migration hiện có.
2. Flow 1: tạo/tra cứu member → package → subscription pending → invoice.
3. Flow 3 phần lõi: receptionist thu tiền → payment/invoice đồng bộ → kích hoạt subscription.
4. Flow 2: manager cấu hình class/schedule/coach → member enrollment/booking với capacity và chống trùng.
5. Flow 3 báo cáo: paid/refund → lọc kỳ/center → đối soát invoice/payment.
6. Bổ sung authorization/audit và test happy/unhappy cases cho từng lát trước khi mở rộng.

## 11. Traceability sơ bộ

| Yêu cầu vai trò | Flow | Entity nền hiện có |
|---|---|---|
| Manager quản lý thành viên, nhân sự, gói | F1 | User, MemberProfile, CoachProfile, StaffProfile, MembershipPackage, MemberSubscription |
| Member đăng ký/cập nhật, mua/gia hạn gói | F1 | User, MemberProfile, MembershipPackage, MemberSubscription |
| Manager quản lý lớp, bộ môn, phòng, lịch, coach | F2 | ClassEntity, Sport, Room, ClassSchedule, ClassSession, ClassCoach |
| Member đăng ký/hủy lớp; Coach xem lịch/roster | F2 | ClassEnrollment, SessionBooking, ClassWaitlist, Attendance |
| Receptionist thanh toán/xuất hóa đơn; Manager báo cáo doanh thu | F3 | Invoice, InvoiceItem, Payment, AuditLog |
