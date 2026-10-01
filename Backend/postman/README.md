# Postman collection

## Import và chạy

1. Chạy API theo hướng dẫn trong `Backend/README.md`.
2. Trong Postman, import cả hai tệp JSON trong thư mục này.
3. Chọn environment **Sports Center Management - Local**.
4. Gửi `Health check (/health)` để xác nhận URL API.

Mặc định `baseUrl` là `http://localhost:54162`, lấy từ launch profile hiện tại. Nếu ứng dụng chạy trên cổng khác, chỉ cần thay đổi biến `baseUrl` trong environment.

## Biến môi trường

| Biến | Mặc định | Mục đích |
|---|---:|---|
| `baseUrl` | `http://localhost:54162` | URL gốc API local |
| `centerId` | `1` | Trung tâm seed mẫu |
| `token` | *(empty)* | Bearer token lấy từ `/api/auth/login` |
| `packageId` | `1` | Gói tập seed mẫu |
| `bankCode` | `VNBANK` | Mã ngân hàng tùy chọn của VNPay |

## Lưu ý thanh toán

- `Create VNPay payment URL` cần bearer token của Member. Không hỗ trợ giả danh member qua `X-Member-Id`.
- Request tạo payment tái sử dụng invoice đang chờ của member/gói tương ứng để tránh tạo hóa đơn trùng khi retry.
- Request callback trong collection là negative test an toàn (thiếu signature). Để kiểm tra callback thành công, cần thực hiện luồng trên VNPay Sandbox để nhận query parameters đã được VNPay ký.
- Không lưu `VnPay:HashSecret` vào environment hoặc collection Postman.
