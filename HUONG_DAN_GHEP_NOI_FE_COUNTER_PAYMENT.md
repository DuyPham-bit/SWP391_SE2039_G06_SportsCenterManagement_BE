# Tích hợp thanh toán tại quầy

Tài liệu này mô tả hợp đồng hiện tại của backend .NET 10. Thanh toán tại quầy hỗ trợ `CASH` và `POS`; checkout tại quầy không nhận `VIETQR`, `MOMO` hoặc `VNPAY` trong endpoint này.

## Endpoint

| Nghiệp vụ | Method và route | Role |
|---|---|---|
| Đăng nhập | `POST /api/auth/login` | Public |
| Tạo hóa đơn và thu tại quầy | `POST /api/payments/counter-checkout` | Receptionist, Manager, Admin |
| Thu tiếp số dư hóa đơn | `POST /api/invoices/{invoiceNumber}/payments` | Receptionist, Manager, Admin |
| Xem hóa đơn/biên nhận | `GET /api/invoices/{invoiceNumber}` | Hội viên sở hữu, nhân sự cùng center, Admin |
| Void hóa đơn chưa thu tiền | `POST /api/payments/counter-void/{invoiceNumber}` | Manager, Admin |

Mọi request đã đăng nhập gửi `Authorization: Bearer <accessToken>`. Hai endpoint thu tiền cần `Idempotency-Key` duy nhất cho thao tác. Không gửi `X-Member-Id` hoặc `X-Staff-Id`; backend lấy actor/member/center từ JWT.

## Tạo hóa đơn và thu tại quầy

```http
POST /api/payments/counter-checkout
Authorization: Bearer <accessToken>
Idempotency-Key: 6aacd390-2202-4326-a1fa-6c2b10104d60
Content-Type: application/json
```

```json
{
  "memberId": 1,
  "packageId": 1,
  "paymentMethod": "CASH",
  "amountReceived": 700000,
  "posApprovalCode": null,
  "note": "Thu tiền tại quầy"
}
```

`paymentMethod` chỉ nhận `CASH` hoặc `POS`. `amountReceived` là số tiền khách đưa. CASH được thu một phần hoặc thừa tiền; backend chỉ ghi số tiền đến hạn, tính tiền thối và chỉ kích hoạt subscription khi hóa đơn đã được trả đủ. POS cần mã chuẩn chi và phải thu đúng toàn bộ giá gói. Giá và thời hạn luôn lấy từ database.

Phản hồi thành công gồm `invoiceNumber`, `invoiceStatus`, `amount`, `amountReceived`, `amountPaid`, `outstandingBalance`, `changeDue`, thông tin hội viên và gói. Có thể dùng `invoiceNumber` để gọi GET hóa đơn và in/xuất lại biên nhận.

## Thu tiếp hóa đơn

```http
POST /api/invoices/SC-20261006090123-0123456789ab/payments
Authorization: Bearer <accessToken>
Idempotency-Key: 78a8d483-f747-4bf1-91ce-f97a58427c24
Content-Type: application/json
```

```json
{
  "amount": 200000,
  "paymentMethod": "CASH",
  "posApprovalCode": null,
  "note": "Thu phần còn lại"
}
```

Endpoint từ chối hóa đơn không tồn tại, sai center, đã void/refund/paid, số tiền không dương hoặc vượt số dư. POS phải có mã chuẩn chi và thu đúng số dư còn lại.

## Void, refund và kết quả lỗi

Void chỉ áp dụng cho hóa đơn chưa nhận payment thành công. Nếu đã nhận tiền, hệ thống từ chối void để giữ lịch sử và yêu cầu Manager/Admin dùng quy trình refund. Không có grace period 15 phút tại backend.

Validation trả `400`, chưa đăng nhập trả `401`, sai role trả `403`, trạng thái/duplicate conflict trả `409`. Deadlock cơ sở dữ liệu trả `503` kèm `Retry-After`; lỗi ngoài dự kiến trả Problem Details với `traceId`. Frontend không nên giả định lỗi có trường `message`.

Nếu client timeout sau khi gửi lệnh thu, gửi lại đúng `Idempotency-Key` và cùng request body để nhận kết quả đã lưu. Không tạo key mới cho lần retry chưa rõ kết quả.

## Cổng thanh toán trực tuyến

Member dùng riêng `POST /api/payments/create-vnpay-url` hoặc `POST /api/payments/create-momo-url` với JWT role `Member` và `Idempotency-Key`. Callback/IPN của cổng là endpoint public nhưng bắt buộc chữ ký hợp lệ, mã tham chiếu và số tiền phải khớp. Payment chưa rõ trạng thái được giữ `Pending`; Manager/Admin phải đối soát qua `/api/payments/{gatewayReference}/reconcile` trước khi có thể thử lại.
