# Phạm vi truy cập media

API giữ nguyên route và cấu trúc response. Quyền được xác định từ tài khoản, role và profile đang có trong database; client không được tự chọn center.

| Thao tác | Phạm vi |
| --- | --- |
| Danh sách, ảnh gần đây, số lượng media | Admin xem toàn hệ thống; tài khoản khác chỉ xem center của mình. Tài khoản chưa có center chỉ xem file chưa gán center do chính mình upload. |
| Upload ảnh dashboard, chứng từ hoàn tiền R2 | Admin hoặc Manager đang được phân công center. Metadata lưu người upload và center tại thời điểm upload. |
| Xóa ảnh dashboard, chứng từ hoàn tiền R2 | Admin hoặc Manager cùng center với file. Chứng từ đã gắn hóa đơn ở center khác bị từ chối. |
| Avatar R2 | Chỉ chủ tài khoản được upload/xóa avatar của mình. |
| Gắn chứng từ khi duyệt hoàn tiền | Manager chỉ dùng upload có metadata thuộc center của hóa đơn; key chưa xác định nguồn phải upload lại. Admin giữ quyền xử lý toàn hệ thống. |

Ảnh avatar/chứng từ R2 không được trộn vào thư viện media dùng chung. Khi Drive lỗi, thư viện dự phòng vẫn áp dụng cùng bộ lọc. Đồng bộ thư mục Drive không tự gán file chưa biết chủ cho người gọi; chỉ Admin được nhập các file đó, với center/người upload để trống.

## Migration và dữ liệu cũ

Migration `20261031090000_AddMediaCenterScope` thêm `uploaded_media.center_id` và hai index. File cũ có người upload được gán center theo profile hiện tại (staff đang active, coach đang active, rồi member); đây là thông tin có sẵn, không khôi phục được lịch sử chuyển center. File không xác định được center chỉ Admin thấy, trừ file chưa gán center do chính người gọi upload.

Metadata mới giữ nguyên center khi nhân viên chuyển nơi làm việc. Chứng từ R2 cũ chưa có metadata chỉ được Manager xóa khi các hóa đơn đang tham chiếu key/URL đó đều thuộc center của mình. Key cũ chưa gắn hóa đơn và ảnh dashboard cũ không rõ chủ chỉ Admin được xóa.

Chạy migration trước khi phục vụ backend mới, bằng lệnh EF trong `Backend/README.md` hoặc cơ chế migrate khi khởi động hiện có. Thay đổi này không xóa object đã lưu và không thay đổi cách phục vụ URL ảnh công khai.

## Kiểm tra

`Backend/verification/LogicAudit` chạy API thật và SQL Server LocalDB tạm, thay S3/Drive bằng bộ ghi nhận tại ranh giới dịch vụ. Các ca kiểm tra gồm vượt quyền giữa hai center, key giả/casing khác, file cũ, chuyển center, gắn chứng từ, thư viện dự phòng và đồng bộ Drive. Không truy cập bucket thật.
