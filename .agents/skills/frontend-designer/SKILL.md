---
name: frontend-designer
description: Chuyển hóa ý tưởng UI thành component code chuẩn mực, responsive và dễ bảo trì.
---

# Technical Standards

1. **Kiến trúc Component**:
   - Chia nhỏ atomic: Layout -> Widgets -> Atoms.
   - Quản lý trạng thái logic tách biệt khỏi presentation (Custom hooks + Presentational components).

2. **Styling & Motion**:
   - Dùng Tailwind CSS với naming logic, hạn chế arbitrary values (`w-[347px]`).
   - Animation có ý nghĩa (Enter/Exit, Hover micro-interactions bằng Framer Motion, spring physics thay vì ease-linear cứng nhắc).
   - Đảm bảo Responsive hoàn chỉnh: Mobile, Tablet, Desktop.
