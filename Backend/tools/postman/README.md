# Postman collection

## Import và chạy

1. Chạy API theo hướng dẫn trong `Backend/README.md`.
2. Trong Postman, import collection và environment **Local** để chạy các ví dụ luồng có sẵn.
3. Để có danh sách đầy đủ route từ API đang chạy, dùng environment PowerShell tại thư mục này:

   ```powershell
   .\Generate-FullApiSmokeCollection.ps1
   ```

   Import thêm `SportsCenterManagement.full.postman_collection.json` và `SportsCenterManagement.full.postman_environment.json`, sau đó chọn environment **Sports Center Management - Full Route Smoke**. Script đọc OpenAPI từ `baseUrl/swagger/v1/swagger.json`; truyền `-BaseUrl` nếu API chạy ở cổng khác.
4. Gửi `Health check (/health)` trong collection ví dụ để xác nhận URL API.

Mặc định `baseUrl` là `http://localhost:54162`, lấy từ launch profile hiện tại. Nếu ứng dụng chạy trên cổng khác, chỉ cần thay đổi biến `baseUrl` trong environment.

## Biến môi trường

| Biến | Mặc định | Mục đích |
|---|---:|---|
| `baseUrl` | `http://localhost:54162` | URL gốc API local |
| `centerId` | `1` | Trung tâm seed mẫu |
| `token` | *(empty)* | Bearer token lấy từ `/api/auth/login` |
| `packageId` | `1` | Gói tập seed mẫu |
| `bankCode` | `VNBANK` | Mã ngân hàng tùy chọn của VNPay |

Collection đầy đủ được tạo động để phản ánh chính xác các operation OpenAPI của instance đang chạy. Giá trị mặc định cho ID là `0`; request dùng payload `{}`. Các phản hồi 4xx được chấp nhận với ID giả, payload chưa hoàn chỉnh hoặc thiếu token. Hãy thay payload/ID bằng dữ liệu được phép trước khi chạy luồng nghiệp vụ; không dùng collection này như bộ dữ liệu production.

## Lưu ý thanh toán

- `Create VNPay payment URL` cần bearer token của Member. Không hỗ trợ giả danh member qua `X-Member-Id`.
- Request tạo payment tái sử dụng invoice đang chờ của member/gói tương ứng để tránh tạo hóa đơn trùng khi retry.
- Request callback trong collection là negative test an toàn (thiếu signature). Để kiểm tra callback thành công, cần thực hiện luồng trên VNPay Sandbox để nhận query parameters đã được VNPay ký.
- Không lưu `VnPay:HashSecret` vào Postman environment/collection hoặc file cấu hình được commit. Cấu hình secret cho backend bằng biến môi trường `VnPay__HashSecret` hoặc User Secrets.
