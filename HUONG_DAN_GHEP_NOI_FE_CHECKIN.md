# Check-in tai quay

Nhanh `feature/thinh-checkin` duoc tach tu `origin/main`, dung chung Payment va Login/JWT da co tren main.

## API

```http
GET /api/centers/1/checkins/eligibility?memberCode=MB001
GET /api/centers/1/checkins/eligibility?memberId=1
POST /api/centers/1/checkins
GET /api/centers/1/checkins?date=2026-10-09
Authorization: Bearer <accessToken>
```

Body POST: `{ "memberCode": "MB001" }` hoac `{ "memberId": 1 }`. Lay accessToken tu `POST /api/auth/login` voi `{ "email": "<email>", "password": "<password>" }`.

- Quyen: Receptionist, Manager, CenterManager, Admin. Backend kiem tra tai khoan, role va center trong DB.
- `memberId` trong body la ID ho so hoi vien can check-in, khong phai UserId cua nhan vien.
- Identity nhan vien thu tu JWT; `X-Staff-Id` bi bo qua va khong the thay JWT.
- Hoi vien can tai khoan Active va goi Active con han tai center trong ngay nghiep vu Viet Nam UTC+7.
- Check-in lap trong ngay tra lai record cu voi `isDuplicate = true`; thao tac dau ghi audit log.
- Timestamp tra UTC; `businessDate` dung ngay Viet Nam. Danh sach trong ngay toi da 500 dong.
- `401`: thieu/sai/het han JWT; `403`: sai quyen/co so; `404`: khong co hoi vien Active; `400`: body sai/khong du dieu kien.

## Kiem thu

```powershell
cd Backend
dotnet test SWP391_SE2039_G06_SportsCenterManagement1.slnx
```

Test HTTP dung SQLite in-memory de chay Login/JWT thuc; test service dung EF InMemory. Cac case gom role/center, goi het han, request lap va audit log. Can kiem thu dong thoi tren SQL Server truoc khi xac nhan ve transaction isolation tren production.
