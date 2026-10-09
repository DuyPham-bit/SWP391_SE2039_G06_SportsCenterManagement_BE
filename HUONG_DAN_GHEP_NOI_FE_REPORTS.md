# Bao cao doanh thu chi tiet

Nhanh `feature/thinh-reports` duoc tach tu `origin/main`. Payment, Login, bao cao tong hop, membership va classes da co tren main duoc giu nguyen.

## API

```http
GET /api/reports/revenue/details?centerId=1&from=2026-10-01&to=2026-10-09&groupBy=day&includeTransactions=true
Authorization: Bearer <accessToken>
```

Lay `accessToken` tu `POST /api/auth/login` voi `{ "email": "<email>", "password": "<password>" }`.

- `centerId` bat buoc; `from`/`to` mac dinh 30 ngay gan nhat; `groupBy` nhan `day`/`month`.
- Quyen: Admin, Manager, CenterManager. Nhan vien phai thuoc center dang xem; backend kiem tra role, lock va center trong DB.
- `401`: thieu/sai/het han token; `403`: sai quyen/co so; `400`: query khong hop le.
- Response co grossRevenue, refundAmount, netRevenue, so giao dich, hoa don, averageTicket, periods, paymentMethods, packages va transactions.
- Tong tien dung logic bao cao tai chinh hien huu. Refund doc tu `payment_refunds` co `Status = Succeeded`, tinh theo `ProcessedAt`, ke ca hoan mot phan/hoan vao ngay khac ngay thu tien.
- Voided/Failed/Pending khong cong vao gross. Transactions toi da 200 dong; dong refund co `refundId`, `paymentStatus = Refunded`, `paidAtUtc` la thoi diem xu ly refund.
- API cu `/api/reports/revenue`, `/api/reports/membership`, `/api/reports/classes` duoc giu nguyen. FE moi dung `/revenue/details` cho dashboard chi tiet.
- Khong dung `X-Staff-Id`/`X-Member-Id` de xac thuc.

## Kiem thu

```powershell
cd Backend
dotnet test SWP391_SE2039_G06_SportsCenterManagement1.slnx
```

Test HTTP dung SQLite in-memory de chay Login/JWT thuc va atomic update cua main; khong gui giao dich sang gateway. Can kiem thu them tren SQL Server truoc khi trien khai.
