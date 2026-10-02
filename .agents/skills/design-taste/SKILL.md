---
name: design-taste
description: Định hướng thẩm mỹ cao cấp, tinh chỉnh visual hierarchy, màu sắc và typography cho giao diện.
---

# Design Philosophy & Rules

1. **Chống "AI Slop"**:
   - Tuyệt đối không dùng gradient tím-hồng generic trừ khi được yêu cầu.
   - Tránh bo góc quá đà (pill-shapes bừa bãi); ưu tiên radius tinh tế (`rounded-lg`, `rounded-xl`).
   - Hạn chế đổ bóng mờ ảo xám xịt; dùng border sắc nét (`border-black/5` hoặc `border-white/10`) kết hợp shadow nhiều lớp có chủ đích.

2. **Typography & Nhịp điệu (Rhythm)**:
   - Ưu tiên font có cá tính phù hợp ngữ cảnh (Sans hình học mạnh mẽ cho thể thao/tech, Serif tinh tế cho editorial/luxury).
   - Thiết lập cấp bậc tương phản rõ rệt: Heading to đậm, body text màu dịu (`text-zinc-600` / `text-zinc-400`), metadata nhỏ tinh tế.

3. **Màu sắc & Điểm nhấn (Accents)**:
   - Quy tắc 60-30-10: 60% nền trung tính, 30% cấu trúc thẻ/khung, 10% màu accent rực rỡ để dẫn mắt người dùng.
   - Khoảng trắng (White space) là một thành phần thiết kế, không phải khoảng trống cần lấp đầy.
