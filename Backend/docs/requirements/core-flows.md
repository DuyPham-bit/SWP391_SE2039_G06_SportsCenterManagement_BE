# Đặc tả nháp: Ba luồng nghiệp vụ bắt buộc

**Trạng thái:** Draft – cần xác nhận chính sách nghiệp vụ trước khi chốt schema/API.  
**Phạm vi:** Flow 1 User & Membership Management; Flow 2 Class Booking & Schedule Management; Flow 3 Payment & Report Management.  
**Nguồn:** Yêu cầu vai trò Center Manager, Coach, Member, Receptionist do người dùng cung cấp; đối chiếu entities và EF Core DbContext hiện có.

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

**Quy tắc phân quyền nền:** API phải kiểm tra role và phạm vi center cho mọi thao tác; không dựa vào việc frontend ẩn nút. Không trả password hash hoặc dữ liệu nhạy cảm qua API.

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
| Invoice | `Draft`, `Issued`, `PartiallyPaid`, `Paid`, `Voided`, `Refunded` |
| Payment | `Pending`, `Succeeded`, `Failed`, `RefundPending`, `Refunded`, `Voided` |

## 4. Flow 1 — User and Membership Management

### 4.1 Mục tiêu

Tạo hoặc quản lý tài khoản và hồ sơ thành viên; chọn, mua hoặc gia hạn gói; chỉ kích hoạt quyền lợi thành viên khi đáp ứng chính sách thanh toán.

### 4.2 Luồng chính: Member tự đăng ký và mua gói

1. Member gửi thông tin đăng ký: họ tên, email, số điện thoại, mật khẩu và (nếu cần) center.
2. Hệ thống validate dữ liệu, chuẩn hóa email/số điện thoại và kiểm tra trùng.
3. Hệ thống tạo User với role Member và trạng thái theo chính sách xác minh; tạo MemberProfile và mã thành viên.
4. Member xem danh sách gói đang bán tại center; chọn một gói và gửi yêu cầu mua.
5. Hệ thống kiểm tra gói còn Active, thuộc center hợp lệ và điều kiện mua/gia hạn.
6. Hệ thống tạo MemberSubscription ở `PendingPayment`, chốt snapshot giá/thời hạn, tạo Invoice và InvoiceItem.
7. Flow 3 ghi nhận thanh toán. Khi thanh toán thành công, hệ thống kích hoạt subscription, thiết lập ngày bắt đầu/kết thúc và gửi xác nhận.
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
| F1-H4 | Member gia hạn khi gói hiện tại còn hiệu lực. | Gia hạn theo chính sách được chốt (nối tiếp ngày hết hạn hoặc bắt đầu ngay); không làm mất lịch sử subscription cũ. |
| F1-H5 | Manager vô hiệu hóa một gói. | Gói không xuất hiện trong lựa chọn mua mới; các subscription đã mua và hóa đơn cũ vẫn giữ nguyên lịch sử. |

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

### 4.6 Acceptance criteria chính

- **AC-F1-01:** Given email đã tồn tại, when đăng ký, then API trả conflict/validation error và không tạo bản ghi mới.
- **AC-F1-02:** Given gói không Active, when member/receptionist mua, then hệ thống từ chối và không tạo invoice/subscription.
- **AC-F1-03:** Given invoice chưa thanh toán đủ, when truy vấn quyền lợi gói, then subscription không được coi là Active.
- **AC-F1-04:** Given thanh toán thành công, when xử lý hoàn tất, then subscription và invoice được cập nhật nhất quán, có ngày hiệu lực và ngày hết hạn.
- **AC-F1-05:** Given receptionist tạo/gia hạn gói, then actor và thời điểm thao tác được lưu để audit.

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

### 5.5 Unhappy cases / ngoại lệ

| ID | Tình huống | Xử lý mong đợi |
|---|---|---|
| F2-U1 | Gói hết hạn, chưa thanh toán hoặc không có quyền vào lớp. | Từ chối đăng ký; trả lý do nghiệp vụ; không giữ chỗ. |
| F2-U2 | Lớp/session hủy, chưa Published, đóng đăng ký hoặc đã bắt đầu. | Từ chối booking; trả trạng thái hiện tại. |
| F2-U3 | Lớp đầy và không bật waitlist. | Từ chối với lỗi capacity; không tạo booking xác nhận. |
| F2-U4 | Hai người cùng đặt chỗ cuối cùng. | Chỉ một request thành công; request còn lại nhận full/waitlist; xử lý atomic ở DB để không vượt sức chứa. |
| F2-U5 | Member đã đăng ký cùng lớp/session hoặc gửi lại request. | Không tạo duplicate; trả booking hiện tại hoặc lỗi conflict theo API contract. |
| F2-U6 | Coach hoặc phòng bị trùng lịch. | Không cho Publish/lưu lịch bị xung đột; trả đối tượng và khung giờ xung đột. |
| F2-U7 | Coach không được phân công vào lớp hoặc member xem dữ liệu người khác. | Trả 403/404 theo chính sách; không lộ roster ngoài phạm vi. |
| F2-U8 | Member hủy sau deadline hoặc session đã diễn ra. | Từ chối hủy tự phục vụ hoặc chuyển sang yêu cầu hỗ trợ; giữ nguyên attendance và lịch sử. |
| F2-U9 | Đổi lịch sau khi đã có booking. | Cập nhật session theo chính sách; thông báo người bị ảnh hưởng; không âm thầm chuyển booking sang lịch khác. |

### 5.6 Acceptance criteria chính

- **AC-F2-01:** Given member không có subscription hợp lệ, when đăng ký lớp yêu cầu gói, then booking bị từ chối và không chiếm chỗ.
- **AC-F2-02:** Given session còn một chỗ và có hai request đồng thời, then tối đa một booking được xác nhận.
- **AC-F2-03:** Given member đã booking một session, when gửi lại cùng yêu cầu, then không phát sinh booking thứ hai.
- **AC-F2-04:** Given coach/phòng có session trùng thời gian, when manager Publish lịch mới, then hệ thống từ chối và nêu xung đột.
- **AC-F2-05:** Given coach được gán vào lớp, when xem lịch, then chỉ thấy lớp/session được phép truy cập.
- **AC-F2-06:** Given booking bị hủy hợp lệ, then chỗ được giải phóng đúng một lần và trạng thái cũ được lưu.

## 6. Flow 3 — Payment and Report Management

### 6.1 Mục tiêu

Tạo khoản phải thu có chi tiết và giá tại thời điểm bán; ghi nhận một hoặc nhiều lần thanh toán; phát hành hóa đơn/biên nhận; tổng hợp báo cáo doanh thu từ giao dịch đã thành công.

### 6.2 Luồng thanh toán

1. Flow 1/2 tạo yêu cầu tính phí (ví dụ bán gói; chỉ thu phí lớp nếu chính sách xác nhận) và gửi danh sách sản phẩm/dịch vụ.
2. Backend lấy giá hiện hành từ DB, áp dụng discount/tax theo quyền và chính sách; không tin giá client gửi lên.
3. Tạo Invoice và InvoiceItem với snapshot mô tả, quantity, unit price, amount, subtotal/discount/tax/total; trạng thái `Issued`.
4. Member thanh toán trực tuyến hoặc receptionist ghi nhận tiền mặt/phương thức được cho phép.
5. Với cổng thanh toán, callback phải xác minh chữ ký/trạng thái và idempotency; với tiền mặt, lưu người thu và thời gian.
6. Lưu Payment. Cập nhật tổng đã trả và Invoice thành `Paid` hoặc `PartiallyPaid`.
7. Khi đủ tiền, kích hoạt subscription/booking có điều kiện thanh toán; phát hành mã/số hóa đơn và cho phép xem/in/xuất.
8. Manager lọc báo cáo theo center và khoảng thời gian; doanh thu lấy từ payment thành công theo ngày ghi nhận, trừ refund/void theo quy tắc báo cáo.

### 6.3 Happy cases

| ID | Tình huống | Kết quả mong đợi |
|---|---|---|
| F3-H1 | Member thanh toán đủ hóa đơn mua gói. | Payment `Succeeded`, Invoice `Paid`, subscription được Active; số tiền khớp total. |
| F3-H2 | Receptionist thu tiền mặt. | Payment có method/cashier/time/amount; invoice cập nhật; receipt có thể tra cứu/in. |
| F3-H3 | Hóa đơn được trả nhiều lần. | Các payment được lưu riêng; invoice `PartiallyPaid` cho đến khi tổng thành công đạt total. |
| F3-H4 | Manager xem doanh thu theo ngày/tháng/center. | Tổng hợp chỉ lấy giao dịch thành công, lọc đúng múi giờ và center, có thể đối soát về invoice/payment. |
| F3-H5 | Khách yêu cầu xuất lại hóa đơn đã thanh toán. | Trả đúng chứng từ đã lưu, không tạo khoản thu mới. |

### 6.4 Unhappy cases / ngoại lệ

| ID | Tình huống | Xử lý mong đợi |
|---|---|---|
| F3-U1 | Thanh toán thất bại/hủy/timeout. | Lưu trạng thái thất bại hoặc pending để tra soát; không kích hoạt quyền lợi; cho phép retry không nhân đôi khoản thu. |
| F3-U2 | Callback gửi lặp hoặc client retry request. | Xử lý idempotent; chỉ ghi nhận một payment thành công cho transaction ngoài. |
| F3-U3 | Callback amount/currency/invoice không khớp hoặc chữ ký sai. | Từ chối cập nhật; ghi log an toàn; không đánh dấu invoice Paid. |
| F3-U4 | Thanh toán vượt số dư còn phải trả. | Từ chối hoặc xử lý như khoản thừa theo chính sách; không tự sửa amount. |
| F3-U5 | Invoice bị void/đã Paid nhưng nhận thêm lệnh thu. | Không thu tiếp; trả conflict; cần quy trình hoàn tiền nếu đã thu ngoài hệ thống. |
| F3-U6 | Refund được thực hiện. | Lưu giao dịch refund và người duyệt; không xóa payment gốc; cập nhật số ròng và trạng thái invoice theo chính sách. |
| F3-U7 | Giao dịch chưa rõ trạng thái do cổng thanh toán timeout. | Giữ pending và đối soát; không giả định thất bại hoặc thành công chỉ từ timeout. |
| F3-U8 | Nhân viên không có quyền xem báo cáo hoặc ghi payment ở center khác. | Trả 403 và không trả dữ liệu tài chính. |

### 6.5 Acceptance criteria chính

- **AC-F3-01:** Given client gửi unit price khác giá trong DB, when tạo invoice, then backend dùng giá DB hoặc từ chối; không dùng giá client.
- **AC-F3-02:** Given tổng payment thành công bằng total invoice, then invoice chuyển Paid đúng một lần và entitlement liên quan được kích hoạt.
- **AC-F3-03:** Given callback thành công gửi lại nhiều lần với cùng transaction code, then chỉ có một giao dịch được tính doanh thu.
- **AC-F3-04:** Given payment thất bại hoặc pending, then báo cáo doanh thu không cộng khoản đó.
- **AC-F3-05:** Given refund, then payment gốc không bị xóa và báo cáo thể hiện doanh thu gộp/refund/ròng theo kỳ.
- **AC-F3-06:** Given khoảng ngày báo cáo [from, to], then điều kiện ngày áp dụng nhất quán theo timezone trung tâm và có thể đối soát với danh sách giao dịch.

## 7. Đánh giá DB hiện có và đề xuất

### 7.1 Đã có nền tảng phù hợp

Trong `SportsCenterManagement.Models/SportsCenterEntities.cs` và `SportsCenterManagement.Services/SportsCenterDbContext.cs` hiện có entities/DbSets cho:

- Tài khoản/quyền: `User`, `Role`, `Permission`, `RolePermission`, `MemberProfile`, `CoachProfile`, `StaffProfile`, `AuditLog`.
- Gói: `MembershipPackage`, `MemberSubscription`.
- Lớp/lịch: `Center`, `Sport`, `Room`, `ClassEntity`, `ClassCoach`, `ClassSchedule`, `ClassSession`, `ClassEnrollment`, `SessionBooking`, `ClassWaitlist`, `Attendance`.
- Thu tiền: `Invoice`, `InvoiceItem`, `Payment`.

Có unique index cho username/email và một số mã; có FK cấu hình bằng EF Core. Do đó không nên tạo lại các bảng này trước khi kiểm tra đầy đủ cột, unique constraint, check constraint, quan hệ và cách dùng trạng thái.

### 7.2 Cần rà soát/bổ sung trước khi chốt migrations

1. **Enrollment vs session booking:** Chốt đăng ký là ghi danh cả lớp hay đặt từng buổi. Nếu hỗ trợ cả hai, quy định một nguồn sự thật và tránh đếm capacity hai lần. Xác định `ClassEnrollment` là roster khóa học, còn `SessionBooking` là đặt từng session.
2. **Ràng buộc chống trùng:** Bổ sung unique `(ClassId, MemberId)` phù hợp cho enrollment lịch sử (cân nhắc filtered unique theo trạng thái nếu cho phép hủy rồi đăng ký lại); unique `(SessionId, MemberId)` cho booking; unique `(SessionId, MemberId)` cho Attendance. Tránh unique đơn giản nếu cần lưu nhiều lần hủy/đăng ký; cân nhắc bảng lịch sử riêng hoặc tái sử dụng bản ghi theo policy.
3. **Capacity concurrency:** Thêm `rowversion` hoặc cập nhật có điều kiện/transaction phù hợp để hai request không chiếm cùng chỗ cuối. Nên có unique/constraint và xử lý concurrency trong service.
4. **Recurring schedule:** Kiểm tra ClassSchedule có day-of-week, effective start/end, timezone, capacity và cờ waitlist hay không. Nếu thiếu, bổ sung; ClassSession phải chứa ngày/giờ cụ thể và snapshot room/coach/status.
5. **Coach assignment:** `ClassCoach` hiện là quan hệ nhiều-nhiều; cần hiệu lực từ/đến hoặc lịch sử assignment nếu coach thay đổi. Phải validate xung đột theo từng session, không chỉ theo lớp.
6. **Membership snapshot:** `MemberSubscription` cần lưu giá thực trả, ngày bắt đầu/kết thúc, người bán, trạng thái, invoice liên quan và quy tắc gia hạn. Package price/duration có thể đổi; lịch sử subscription không được đổi theo giá package hiện tại.
7. **Invoice snapshot:** `InvoiceItem` đang có mô tả/quantity/unit price/amount. Đảm bảo tính tiền decimal chính xác, quy tắc discount/tax, currency, issued/due time và không phụ thuộc giá hiện tại của package.
8. **Payment/refund:** Không dùng payment `PaidAt` bắt buộc cho trạng thái pending/failed; đổi thành `CreatedAt` và nullable `PaidAt`/`ProcessedAt` phù hợp. Thêm unique idempotency key hoặc provider transaction key có unique constraint; refund nên là giao dịch riêng hoặc bảng PaymentTransaction, không ghi đè giao dịch gốc.
9. **Quan hệ actor:** `Payment.ProcessedBy` và `Invoice.CreatedBy` cần FK tới User; xác thực actor đúng center/role. Làm rõ `Payment.MemberId` có thừa vì suy ra từ Invoice hay cần giữ snapshot; nếu giữ, kiểm tra member nhất quán với invoice.
10. **Báo cáo/audit:** `AuditLog` cần actor, action, entity type/id, timestamp UTC, correlation/request id và metadata đã lọc PII. Báo cáo doanh thu nên query từ payment/refund có trạng thái và thời điểm chuẩn hóa, không chỉ dựa `Invoice.PaidAt`.
11. **Multi-role:** `User.RoleId` chỉ biểu diễn một role tại một thời điểm. Nếu một user có thể vừa là coach vừa là manager/receptionist, đổi sang `UserRoles(UserId, RoleId, CenterId, ...)`; nếu mỗi tài khoản chỉ có đúng một role thì giữ thiết kế hiện tại.
12. **Constraints và indexes:** Thêm check constraints cho `price >= 0`, `duration_days > 0`, `end_time > start_time`, số lượng/sức chứa dương, amount không âm; index cho lọc theo center/date/status/member/session/invoice theo workload.

**Không khuyến nghị** tạo thêm `Transactions` nếu `Payment` đã là giao dịch thu tiền chính. Chỉ thêm bảng riêng khi phải lưu nhiều lần thử, callback, refund hoặc event đối soát độc lập.

## 8. API sườn gợi ý

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
POST   /api/invoices/{id}/payments         # Receptionist/manual method
POST   /api/payment-webhooks/{provider}    # xác minh chữ ký + idempotency
GET    /api/invoices/{id}
GET    /api/reports/revenue?centerId=&from=&to=&groupBy=day|month
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
2. Member tự đăng ký cần xác minh email/OTP trước khi dùng hệ thống không?
3. Gói bắt đầu ngay, theo ngày thanh toán hay ngày nhân viên chọn? Gia hạn khi còn hạn sẽ nối tiếp hay chồng lấn?
4. Gói có giới hạn số buổi/lớp hay chỉ theo thời hạn? Gói nào được vào lớp nào?
5. Đăng ký lớp là theo cả khóa hay từng buổi? Hủy trước bao lâu? Có waitlist và phí hủy không?
6. Thanh toán chỉ tiền mặt hay tích hợp cổng thanh toán? Có thanh toán một phần, refund, discount và thuế không?
7. Doanh thu báo cáo là tiền thu trong kỳ hay giá trị hóa đơn phát hành trong kỳ? Refund ghi âm ở kỳ refund hay điều chỉnh kỳ gốc?
8. Center Manager và Receptionist có thể xem dữ liệu tài chính/PII tới phạm vi nào?

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

