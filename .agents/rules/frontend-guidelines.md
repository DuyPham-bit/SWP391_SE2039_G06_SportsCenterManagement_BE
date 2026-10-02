# Frontend Design & Engineering Guidelines

Tài liệu hướng dẫn phối hợp tiêu chuẩn thẩm mỹ (**design-taste**) và kỹ thuật component (**frontend-designer**) cho toàn bộ giao diện dự án Sports Center Management System (SCMS).

---

## 🎨 1. Định hướng thẩm mỹ cao cấp (Design Taste)

### 1.1 Chống "AI Slop"
- **Không dùng gradient generic:** Tuyệt đối không dùng gradient tím-hồng generic trừ khi được yêu cầu cụ thể.
- **Radius tinh tế:** Tránh bo góc quá đà (không lạm dụng pill-shapes bừa bãi); ưu tiên radius tinh tế (`rounded-lg`, `rounded-xl`).
- **Viền sắc nét thay vì bóng mờ đục:** Hạn chế đổ bóng mờ ảo xám xịt; dùng border sắc nét (`border-black/5` hoặc `border-white/10`) kết hợp shadow nhiều lớp có chủ đích.

### 1.2 Typography & Nhịp điệu (Rhythm)
- **Font chữ phù hợp:** Ưu tiên font có cá tính phù hợp ngữ cảnh thể thao/tech (Sans hình học mạnh mẽ như Inter, Outfit, Plus Jakarta Sans).
- **Phân cấp tương phản rõ rệt:**
  - Heading: To, đậm, sắc nét (`text-zinc-900` / `dark:text-white`, `font-bold` hoặc `font-semibold`).
  - Body text: Màu dịu mắt (`text-zinc-600` / `dark:text-zinc-400`).
  - Metadata / Badge: Nhỏ, tinh tế (`text-xs`, `tracking-wide`, uppercase nhẹ).

### 1.3 Màu sắc & Điểm nhấn (Accents)
- **Quy tắc 60-30-10:**
  - **60% Nền trung tính:** Slate / Zinc / Neutral (`bg-zinc-50` / `dark:bg-zinc-950`).
  - **30% Cấu trúc thẻ/khung:** Card, Sidebar, Navigation (`bg-white` / `dark:bg-zinc-900`, `border-zinc-200/80` / `dark:border-zinc-800`).
  - **10% Accent dẫn hướng mắt:** Màu chủ đạo thể thao mạnh mẽ (Emerald, Indigo, Electric Blue hoặc Amber) cho CTA buttons, active state, status indicators.
- **Khoảng trắng (White space):** Là một thành phần thiết kế quan trọng, giữ layout thoáng đãng, không cố nhồi nhét nội dung.

---

## 💻 2. Tiêu chuẩn kỹ thuật Component (Frontend Designer)

### 2.1 Kiến trúc Component
- **Chia nhỏ Atomic:**
  - `Layout`: Khung trang, Navigation, Sidebar, Footer.
  - `Widgets`: Bảng dữ liệu, Thẻ gói tập, Lịch ca học, Form thanh toán.
  - `Atoms`: Button, Input, Badge, Avatar, Tooltip.
- **Tách biệt Logic & Presentation:**
  - Dùng Custom Hooks để quản lý API call, state, validation (`useMembershipPackages`, `useVNPayPayment`).
  - Presentational Components chỉ nhận props và render UI thuần túy.

### 2.2 Styling & Motion
- **Tailwind CSS chuẩn mực:**
  - Dùng class utility có logic, hạn chế tối đa arbitrary values (`w-[347px]`, `text-[13px]`).
  - Sử dụng spacing tokens chuẩn (`p-4`, `gap-6`, `space-y-4`).
- **Animation có ý nghĩa:**
  - Dùng micro-interactions (Hover, Focus, Active) và transitions mượt mà bằng Framer Motion hoặc Tailwind transitions.
  - Dùng Spring physics tự nhiên thay vì linear/ease-in-out thô cứng.
- **Responsive hoàn chỉnh:**
  - Mobile first: Đảm bảo giao diện hiển thị tối ưu trên Mobile (`sm`), Tablet (`md`), và Desktop (`lg`, `xl`).
