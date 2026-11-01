# Ghép phần sửa payment — 2026-10-13

Nguồn: `SWP391_SE2039_G06_SportsCenterManagement_BE-feature-payment-fix/Backend`.
Đích: `Backend`. Phạm vi: payment và kiểm thử liên quan.

## Thay đổi

- Thêm `POST /api/payments/vietqr/{orderCode}/sync`. Hội viên sở hữu giao dịch gọi API này khi webhook bị lỡ; backend truy vấn PayOS và xác thực chữ ký, mã đơn, số tiền, trạng thái và tham chiếu ngân hàng trước khi ghi nhận thanh toán.
- Giữ ánh xạ `orderCode = Payment.Id`, không dùng `Invoice.Id`. Đồng bộ lặp lại không tạo payment hoặc kích hoạt gói thêm lần nữa. Thanh toán đến sau khi hủy hóa đơn tiếp tục đi qua quy trình `PaidAfterVoid` hiện có.
- Thêm alias webhook `POST /api/payments/payos-webhook`, dùng cùng xử lý với `/api/payments/payos-ipn`.
- Ghép cách tạo dữ liệu ký PayOS có trường `null`, danh sách giao dịch và trường webhook mở rộng; so sánh chữ ký bằng `FixedTimeEquals`.
- Khi một hóa đơn còn payment `Pending`, yêu cầu tạo link với idempotency key mới bị từ chối thay vì tự hủy giao dịch cũ. Hóa đơn chờ quá 15 phút cũng không tự bị hủy khi tìm đơn chờ.
- Đưa bộ test payment của nguồn vào solution, điều chỉnh fixture SQLite theo schema, JWT và startup của bản đích; thêm kiểm tra giữ tương thích với hóa đơn, hủy đơn và thông báo.

## Quyết định tương thích

- Giữ .NET 10, EF Core và SQL Server hiện tại. SQLite chỉ dùng trong bộ test cô lập; không thay database ứng dụng.
- Giữ JWT/role và phân quyền theo center, API liệt kê/hủy hóa đơn chờ, `CancelPendingIfAny` cho yêu cầu hủy có chủ đích, dữ liệu mở rộng trên biên nhận và thông báo kích hoạt gói của bản đích.
- Giữ cột `Payment.AttemptedAt` và các migrations hiện tại. Không chép alias `NotMapped` từ nguồn vì schema đích có cột này.
- Giữ cách xử lý VNPay/MoMo hiện tại, gồm múi giờ tạo URL VNPay và cấu hình URL trả về. Không chép cấu hình, credentials, auth, module khác hoặc frontend từ bản nguồn.
- Giữ các thay đổi cục bộ có sẵn trong repository. Không commit, push hoặc áp dụng migration vào database đang chạy.

## Cách gọi đồng bộ

```http
POST /api/payments/vietqr/{orderCode}/sync
Authorization: Bearer <JWT của hội viên sở hữu giao dịch>
```

Không cần request body. Đọc `success` và `message` trong phản hồi. Không xác minh được nhà cung cấp trả HTTP 503; giao dịch chưa thanh toán không được kích hoạt gói. Frontend hiện chưa tự gọi API đồng bộ mới này.

## Kiểm chứng

Chạy tại `Backend`:

```powershell
dotnet build SWP391_SE2039_G06_SportsCenterManagement1.slnx --no-restore -p:OutputPath=bin/PaymentValidation/
dotnet test SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj --no-restore -p:OutputPath=bin/PaymentValidation/ --logger trx
```

Thư mục đầu ra riêng giúp kiểm chứng khi backend hiện tại đang chạy và khóa executable trong `bin/Debug`. Khởi động lại backend sau khi ghép để nạp code mới. Test dùng HTTP giả lập và SQLite cô lập; chưa kiểm chứng chuyển tiền thật hoặc webhook công khai.

Kết quả ngày 2026-10-13: **72/72 test đạt, 0 lỗi, 0 bỏ qua**. Báo cáo TRX được tạo trong `SportsCenterManagement.Tests/TestResults/`. Test hồi quy chữ ký với trường `null` và danh sách giao dịch đã được xác nhận lỗi trước khi ghép và đạt sau khi ghép.

Đối chiếu SHA-256 với trạng thái trước lần ghép xác nhận 202 file có sẵn ngoài danh sách thay đổi giữ nguyên, không xóa file nào.

## Hoàn tác

Hoàn tác riêng các thay đổi payment và cấu hình test trong lần ghép này; giữ nguyên thay đổi cục bộ có trước. Không cần hoàn tác database vì lần ghép không thay schema hoặc chạy migration trên database ứng dụng.
