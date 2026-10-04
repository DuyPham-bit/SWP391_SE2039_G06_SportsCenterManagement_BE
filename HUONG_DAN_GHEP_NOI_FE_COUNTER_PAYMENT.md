# 📑 TÀI LIỆU HƯỚNG DẪN GHÉP NỐI FRONTEND & BACKEND: MODULE THANH TOÁN TẠI QUẦY (COUNTER CHECKOUT)

> **Dự án:** Hệ thống Quản lý Trung tâm Thể thao (Sports Center Management System - SCMS)  
> **Module:** Đăng Ký & Gia Hạn Gói Tập Tại Quầy Thu Ngân (`CounterMembership.jsx`)  
> **Mục đích tài liệu:** Hướng dẫn đội ngũ Frontend ghép nối API với Backend .NET 10, cấu trúc dữ liệu gửi/nhận, các nút bấm và tính năng cần bổ sung để hoàn thiện trải nghiệm thu ngân.

---

## 🚀 1. DANH SÁCH CÁC API BACKEND CẦN GỌI

| Nghiệp vụ | Phương thức | URL API Endpoint | Mô tả |
| :--- | :---: | :--- | :--- |
| **1. Thanh toán Tiền mặt / POS** | `POST` | `/api/payments/counter-checkout` | Xác thực thu tiền tại quầy, tính tiền thối, kích hoạt gói `Active` và trả về thông tin biên lai. |
| **2. Quét mã VietQR tại quầy** | `POST` | `/api/payments/create-vietqr` | Sinh mã QR VietQR (Napas 247) để khách quét. Webhook PayOS sẽ tự kích hoạt gói khi có tiền vào. |
| **3. Quét Ví MoMo tại quầy** | `POST` | `/api/payments/create-momo-url` | Sinh link / QR MoMo để khách quét ví MoMo. Webhook MoMo IPN tự động kích hoạt gói. |
| **4. Cổng VNPay tại quầy** | `POST` | `/api/payments/create-vnpay-url` | Sinh URL chuyển hướng sang cổng VNPay / quét mã VNPAY-QR. |
| **5. Hủy giao dịch bấm nhầm (Void)** | `POST` | `/api/payments/counter-void/{invoiceNumber}` | Thu hồi gói tập và hủy hóa đơn khi Lễ tân lỡ bấm nhầm (giới hạn trong vòng 15 phút). |

---

## 📦 2. CẤU TRÚC DỮ LIỆU REQUEST & RESPONSE (DATA CONTRACT)

### 2.1. API Thanh Toán Tại Quầy: `POST /api/payments/counter-checkout`

#### 🔹 Request Body (Dữ liệu Frontend gửi lên):
```json
{
  "memberId": 1,
  "packageId": 1,
  "paymentMethod": "TIỀN MẶT", 
  "amountReceived": 700000,
  "posApprovalCode": null,
  "note": "Khách đóng tiền mặt tại quầy ca sáng"
}
```
*Ghi chú các trường:*
- `memberId` *(long, bắt buộc)*: ID của hội viên chọn ở Step 1.
- `packageId` *(long, bắt buộc)*: ID gói tập chọn ở Step 2.
- `paymentMethod` *(string, bắt buộc)*: `"TIỀN MẶT"`, `"CASH"`, hoặc `"THẺ POS"`, `"POS"`.
- `amountReceived` *(decimal)*: Số tiền khách đưa (dùng để Backend tự tính tiền thối).
- `posApprovalCode` *(string, tùy chọn)*: Mã chuẩn chi từ hóa đơn máy POS nếu quẹt thẻ.
- `note` *(string, tùy chọn)*: Ghi chú ca trực.

#### 🔹 Response Body (Dữ liệu Backend trả về khi Thành công - HTTP 200 OK):
```json
{
  "success": true,
  "message": "Thanh toán và kích hoạt gói tập tại quầy thành công!",
  "transactionRef": "SC-20261002084920-00b033949267",
  "amount": 650000,
  "amountReceived": 700000,
  "changeDue": 50000,
  "paymentMethod": "TIỀN MẶT",
  "paidAt": "2026-10-02T08:49:20.420Z",
  "member": {
    "id": 1,
    "fullName": "Nguyễn Văn A",
    "memberCode": "MB00001",
    "packageExpiry": "01/11/2026"
  },
  "package": {
    "id": 1,
    "name": "Gói Basic Thể Thao",
    "durationDays": 30
  }
}
```

#### 🔹 Response Body khi Thất bại (Ví dụ: Khách đưa thiếu tiền - HTTP 400 Bad Request):
```json
{
  "success": false,
  "message": "Số tiền khách đưa (500,000đ) không đủ để thanh toán gói tập (650,000đ)."
}
```

---

### 2.2. API Hủy Giao Dịch Bấm Nhầm: `POST /api/payments/counter-void/{invoiceNumber}`

- **URL Param:** `invoiceNumber` chính là mã `transactionRef` (ví dụ `SC-20261002084920-00b033949267`).
- **Request Body:**
  ```json
  {
    "reason": "Lễ tân chọn nhầm gói tập cho khách"
  }
  ```
- **Response Body (HTTP 200 OK):**
  ```json
  {
    "success": true,
    "message": "Đã hủy thành công giao dịch hóa đơn SC-20261002084920-00b033949267 và thu hồi gói tập.",
    "invoiceNumber": "SC-20261002084920-00b033949267",
    "amount": 650000
  }
  ```

---

## 🛠️ 3. CÁC TÍNH NĂNG VÀ GIAO DIỆN FRONTEND CẦN BỔ SUNG / CHỈNH SỬA

### 📍 Tại STEP 3 (Màn hình Thu Ngân & Chọn Phương Thức):

1. **Bổ sung đầy đủ 5 nút Phương thức thanh toán:**
   - `[💵 TIỀN MẶT]`
   - `[🏦 CHUYỂN KHOẢN VIETQR]`
   - `[🟣 VÍ MOMO]`
   - `[🔵 VNPAY]`
   - `[💳 THẺ POS]`

2. **Box nhập "Số tiền khách đưa" & "Tiền thối lại" (Chỉ hiển thị khi chọn `TIỀN MẶT`):**
   - Ô Input nhập `amountReceived` (Mặc định tự động điền bằng giá gói `selectedPackage.price`).
   - Nút bấm nhanh mệnh giá: `[Đúng số tiền]`, `[+500k]`.
   - Dòng tính tiền thối tự động: `Tiền thối lại: (amountReceived - price) VNĐ`.
   - **Validation:** Nếu `amountReceived < selectedPackage.price` $\rightarrow$ Đổi màu đỏ cảnh báo *"⚠️ Chưa đủ tiền"* và disable nút bấm xác nhận.

3. **Phân nhánh xử lý khi nhấn `[Xác Nhận Đã Thu & Kích Hoạt Thẻ]`:**
   - **Nếu là `TIỀN MẶT` hoặc `THẺ POS`:** 
     + Gọi API `POST /api/payments/counter-checkout`.
     + Thành công $\rightarrow$ Gán `setReceiptData(result)` $\rightarrow$ Chuyển sang `setStep(4)`.
     + Thất bại $\rightarrow$ Ở lại Step 3 và hiện Toast đỏ `showError(err.message)`.
   - **Nếu là `CHUYỂN KHOẢN VIETQR` / `VÍ MOMO` / `VNPAY`:**
     + Gọi API tạo link/QR tương ứng $\rightarrow$ Bật Modal Popup hiện mã QR để khách quét.

---

### 📍 Tại STEP 4 (Màn hình Hóa Đơn & Biên Lai):

1. **Map dữ liệu trả về từ Backend (`receiptData`):**
   - Mã giao dịch: `{receiptData.transactionRef}`
   - Hội viên: `{receiptData.member.fullName} ({receiptData.member.memberCode})`
   - Gói tập: `{receiptData.package.name}`
   - Hạn sử dụng mới: `{receiptData.member.packageExpiry}`
   - Tiền thối đã trả: `{receiptData.changeDue.toLocaleString()} VNĐ` (nếu có)
   - Tổng tiền đã thu: `{receiptData.amount.toLocaleString()} VNĐ`

2. **Thêm nút `[🖨️ In Biên Lai]`:**
   - Thêm nút gọi lệnh `window.print()` của trình duyệt.
   - Thêm CSS `@media print` để khi bấm in, máy in chỉ in đúng khung hóa đơn (`#receipt-print-area`) và tự động ẩn thanh Menu/Sidebar.

3. **Thêm nút `[⚠️ Hủy Giao Dịch (Bấm Nhầm)]`:**
   - Cho phép Lễ tân hủy giao dịch ngay tại Step 4 nếu phát hiện chọn nhầm.
   - Khi bấm $\rightarrow$ Hiện Modal yêu cầu nhập *"Lý do hủy"* $\rightarrow$ Gọi API `counter-void`.
   - Thành công $\rightarrow$ Hiện Toast xanh thông báo đã thu hồi gói và quay về `setStep(1)`.

---

## 💻 4. ĐOẠN CODE MẪU CHO `services/api.js` TRÊN FRONTEND

```javascript
// Thêm vào file services/api.js (hoặc file quản lý API của dự án)
export const receptionApi = {
  // 1. Thanh toán trực tiếp tại quầy (Tiền mặt / POS)
  counterCheckout: async (payload) => {
    const response = await fetch('http://localhost:54162/api/payments/counter-checkout', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        // 'Authorization': `Bearer ${token}` // Gắn JWT Token nếu đã có Auth
      },
      body: JSON.stringify(payload)
    });

    const data = await response.json();
    if (!response.ok) {
      throw new Error(data.message || 'Thanh toán tại quầy thất bại.');
    }
    return data;
  },

  // 2. Hủy giao dịch bấm nhầm
  counterVoid: async (invoiceNumber, reason) => {
    const response = await fetch(`http://localhost:54162/api/payments/counter-void/${invoiceNumber}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ reason })
    });

    const data = await response.json();
    if (!response.ok) {
      throw new Error(data.message || 'Hủy giao dịch thất bại.');
    }
    return data;
  }
};
```

---

## 🎯 5. QUY TẮC BẢO MẬT & BẮT LỖI CẦN NHỚ

1. **Không tin tưởng giá tiền từ Client:** Frontend không cần gửi trường `Price` lên. Backend sẽ tự động truy vấn giá niêm yết trong Database theo `packageId` để đảm bảo chống sửa giá.
2. **Xử lý Grace Period (15 phút):** API Hủy đơn chỉ cho phép Lễ tân tự hủy trong vòng 15 phút kể từ lúc thanh toán. Sau 15 phút, Backend sẽ quăng lỗi yêu cầu quyền Quản lý (Manager).
3. **Chống Spam Click:** Khi Lễ tân bấm `[Xác nhận]`, hãy set state `loading = true` và disable nút bấm ngay lập tức để tránh gửi 2 request trùng nhau.
