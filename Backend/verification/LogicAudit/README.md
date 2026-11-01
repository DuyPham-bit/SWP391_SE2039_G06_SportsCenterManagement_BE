# Kiểm chứng logic BE/FE

Chương trình hồi quy giới hạn cho các lỗi được rà soát ngày 18/10/2026. Dùng service/controller/JWT thật và SQL Server LocalDB, HTTP chỉ lắng nghe localhost. Không gọi cổng thanh toán, Google, AI hay gửi email thật.

Mỗi lần chạy tạo database `SCMS_LogicAudit_<GUID>` mới, đặt file trong thư mục tạm của Windows rồi tự xóa database bằng `finally`. Không đọc/ghi dữ liệu trong database ứng dụng. Không khôi phục solution test BE cũ và không thêm thư viện test.

Schema được tạo bằng chuỗi migration thật. Trước migration bảo mật mới, fixture chèn một tài khoản ở schema liền trước và kiểm tra nâng cấp vẫn giữ dữ liệu, mặc định auth_version bằng 0. Gateway Email giả chỉ lắng nghe loopback, không đọc provider credential hoặc gửi email ra ngoài. Header X-Audit-Client chỉ được middleware của chương trình kiểm chứng dùng để tạo caller IP tách biệt giữa các ca; production không đọc header này.

Chạy từ thư mục workspace:

```powershell
dotnet build Backend/verification/LogicAudit/LogicAudit.csproj -o tmp/logic-audit-runner
dotnet tmp/logic-audit-runner/LogicAudit.dll
```

Yêu cầu Windows, .NET SDK 10 và SQL Server LocalDB instance `MSSQLLocalDB`. Exit code khác 0 nghĩa là còn ca thất bại; log ghi từng ca và tổng kết. Chạy bằng tài khoản Windows sở hữu LocalDB.

Các ca kiểm tra: hủy hóa đơn/chủ sở hữu/phạm vi trung tâm/quyền bị thu hồi, khoản đã thu một phần, snapshot và dư nợ, trạng thái gói từ BE, JWT/HTTP/validation/contract phòng, KPI và trạng thái lịch HLV, ca qua nửa đêm, doanh số khác trung tâm, báo cáo thu/refund/booking, cạnh tranh đặt lịch/nghỉ phép; reset mật khẩu khi thiếu gateway, gửi OTP qua gateway giả, provider lỗi, nhập sai/hết hạn/replay/cấp lại, hai reset đồng thời, giới hạn request, mật khẩu 128 ký tự, thu hồi JWT/OTP sau đổi mật khẩu và hủy request khi Email đang gửi. Kiểm tra đăng ký bắt buộc OTP, cô lập mã theo email/mục đích, cooldown, khóa sau 5 lần sai, cạnh tranh tiêu thụ mã và SMTP loopback nhận MIME/từ chối gửi. Lần xác minh ngày 20/10/2026: 67/67 ca đạt. Hướng dẫn tổng quan: [PROJECT_SUMMARY.md](../../../../docs/PROJECT_SUMMARY.md).
