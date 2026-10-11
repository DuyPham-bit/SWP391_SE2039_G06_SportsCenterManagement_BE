# Flow 2 — Booking and schedule management

## Nguồn và quyết định

- Nguồn: yêu cầu Flow 2 của người dùng ngày 10/10/2026, bốn vai trò Manager, Coach, Member, Receptionist.
- Decision: giữ React 18/Vite, ASP.NET Core .NET 10, EF Core và SQL Server hiện có.
- Decision được người dùng xác nhận: Manager phân quyền **trong trung tâm**, System Admin giữ ma trận quyền toàn hệ thống.
- Phạm vi sửa Flow 2: danh mục lớp/phòng/bộ môn/HLV, lịch và buổi học, công bố/hủy, phân công HLV, đặt/hủy/chờ, hỗ trợ tại quầy, roster và điểm danh.
- Constraint: các chức năng giáo án, kết quả tập luyện, AI, hỗ trợ và thông báo nằm trong danh sách vai trò nhưng chưa được hoàn thiện bởi đợt sửa Flow 2 này. Không coi dữ liệu localStorage/mock là dữ liệu backend đã lưu.

## Thiết kế và hợp đồng

FE phân biệt `Class.Id`, `ClassSession.Id`, `CoachProfile.Id` và `User.Id`. Quản lý dùng catalog lớp kể cả Draft; màn đặt chỗ dùng buổi học cụ thể, không cho đổi ngày tùy ý. Các lựa chọn bộ môn/phòng/HLV lấy từ DB. API là nguồn quyết định cuối về quyền lợi gói, cửa sổ đặt, sức chứa và xung đột.

| Requirement | FE | API / BE |
|---|---|---|
| F2-01 Lớp/phòng và bộ môn | ClassesAndRooms | `GET centers/{id}/classes/all`, `GET centers/{id}/sports`, `GET/POST/PUT centers/{id}/rooms` |
| F2-02 Phân công đúng HLV | CoachAssignment | `GET centers/{id}/coaches`, API phân công hiện có nhận CoachProfile.Id |
| F2-03 Lịch hoạt động | ClassesAndRooms | API schedules/publish/cancel hiện có, tạo buổi từ lịch tuần |
| F2-04 Đặt/hủy và chờ | ClassScheduleView, MyBookings | SessionBookingService; thêm `members/me/session-waitlist` GET/DELETE |
| F2-05 Lễ tân hỗ trợ | CounterBooking | `members/{memberId}/session-bookings` GET/DELETE, `members/{memberId}/class-sessions/{sessionId}/bookings` POST |
| F2-06 HLV xem roster/điểm danh | ClassMembers, Attendance | `coaches/me/sessions/{sessionId}/members` GET, `/attendance` PUT |
| F2-07 Phân quyền theo trung tâm | RolesPermissions, menu từ capabilities | `centers/{id}/roles` GET, `centers/{id}/roles/{roleId}/permissions` PUT, `access/me/capabilities` GET |
| F2-08 Audit | AuditLogs hiện có | Tạo/sửa phòng, đặt/hủy/chờ, điểm danh, đổi quyền ghi actor/center |

### Quy tắc

- Đặt trước tối thiểu 30 phút, tối đa 7 ngày, theo giờ Việt Nam. `isBookable` phản ánh cùng cửa sổ này.
- Backend kiểm tra gói có hiệu lực vào ngày buổi học, số lớp/bộ môn và lịch trùng. FE không dùng trạng thái gói đã cache để chặn thay backend.
- Lớp đầy: chỉ trả confirmed khi `booked=true`; `waitlisted=true` là lượt chờ. Hội viên xem và rời danh sách chờ của mình.
- Hủy dưới 2 giờ là hủy muộn, vẫn tính sử dụng buổi; FE hiển thị message trả về từ backend.
- Lễ tân chỉ thao tác hội viên và buổi thuộc trung tâm được gán; service vẫn kiểm tra ownership lượt đặt.
- HLV chỉ đọc roster/ghi điểm danh buổi được phân công. Không điểm danh buổi chưa bắt đầu hoặc bị hủy; không tự mặc định mọi người có mặt.
- Sửa sức chứa phòng không được thấp hơn số chỗ đã đặt của buổi tương lai.

### Phân quyền trung tâm

Lưu override tại bảng `system_settings` hiện có với key `center.rbac.{centerId}.{roleId}`. Không cần migration/schema mới. JSON chứa `PermissionCodes` và `CapabilityIds`. Không có override thì dùng quyền nền hiện tại. Manager chỉ cấu hình Member/Coach/Receptionist, trong tập quyền nền của vai trò; không cấp `roles.permissions.manage`, không sửa Manager/SystemAdmin hoặc trung tâm khác.

`PermissionAuthorizationHandler` áp dụng override quyền dữ liệu ở backend. `CenterCapabilityAttribute` bảo vệ các thao tác booking, điểm danh, roster, check-in và quầy. Menu nhận capabilities thực tế khi đăng nhập. Thay đổi API có hiệu lực từ request tiếp theo; menu cần tải lại thông tin tài khoản/đăng nhập lại.

Thay quyền và audit cùng transaction Serializable; đặt/hủy dùng transaction hiện có. Các thay đổi trong AuthController.cs và DbInitializer.cs đã có trước đợt này được giữ nguyên.

## Kiểm thử và giới hạn bằng chứng

```powershell
cd Frontend
npm test
npm run build

# Từ workspace root; output riêng tránh khóa executable của API đang chạy.
dotnet build Backend/Backend/SportsCenterManagement.API/SportsCenterManagement.API.csproj --no-restore -o .validation/backend
dotnet run --project Backend/Backend/tests/Flow2.Regression/Flow2.Regression.csproj
```

FE tests dùng HTTP mock để kiểm tra hợp đồng request/response, phân trang, ID, waitlist, hủy muộn và route phân quyền trung tâm. BE regression kiểm tra validation ngày/pagination và ranh giới capability. Đây không phải bằng chứng transaction/concurrency hay E2E với SQL Server.

Kết quả đã chạy: FE 10/10 test pass; BE 10 ca validation và 4 kiểm tra ranh giới quyền pass; FE production build pass (còn cảnh báo bundle lớn và import động/tĩnh); BE build pass, 0 warning, 0 error.

Chưa chạy browser E2E và relational integration cho các endpoint mới. Cần kiểm thử thực tế hai trung tâm, hai vai trò, hai hội viên tranh chỗ cuối, promotion waitlist, rollback audit và thu hồi quyền trong phiên đang đăng nhập. Không tuyên bố các ca này đã pass.

## Vận hành / rollback

Khởi động lại API để nạp endpoint mới, build lại FE và đăng nhập lại để tải capabilities. Không push/release hoặc thay đổi dữ liệu nghiệp vụ thực tế trong đợt sửa này. Hoàn tác code và các key `center.rbac.*` đã cấu hình nếu rollback quyền trung tâm; giữ audit lịch sử. Không xóa dữ liệu đặt chỗ/điểm danh khi rollback code.
