# Payment JWT integration

Branch: `feature/payment-fix`, based on `main` (`cbff81d`).

## Login

Use the existing Duy login flow: `POST /api/auth/login` with `{ "email": "...", "password": "..." }`.
The response contract remains `accessToken`, `userId`, `role`, `expiresAt`. Password validation,
account status checks and the five-failure / 15-minute lockout stay in the existing `AuthService`.

## Payment requests

- Send `Authorization: Bearer <accessToken>` for all user/staff payment APIs.
- Keep `Idempotency-Key` on payment creation, counter checkout and refund requests.
- Online payment resolves `MemberProfile.Id` from the authenticated `UserId`; do not use `userId` as `memberId`.
- Counter payment uses the authenticated staff identity. `X-Member-Id` and `X-Staff-Id` are ignored.
- Account status, lockout, role and active staff center are reloaded from DB for every authenticated request.
- `401` means missing/invalid/expired JWT or revoked account. `403` means insufficient role/staff scope.
  Existing cross-center payment lookups return `404` to conceal data outside the caller's scope.
- VNPay callback/IPN, MoMo callback/IPN and PayOS IPN remain anonymous and validate gateway signatures separately.
- Swagger includes the Bearer JWT authorization control.

## Configuration

Set `Jwt__Key` (at least 32 bytes), `Jwt__Issuer`, `Jwt__Audience`, and optionally `Jwt__ExpiresMinutes` (default 60).
Existing `Jwt:SecretKey` and `Jwt:AccessTokenMinutes` aliases are supported when the primary settings are absent.
JWT issuance and validation share one configuration, require signed HS256 tokens, validate issuer/audience/expiry,
and allow 30 seconds of clock skew. Old DataProtection bearer tokens are no longer accepted; log in again.
Do not commit production keys. Use environment variables or User Secrets.

## Verification

Run `dotnet test Backend/SWP391_SE2039_G06_SportsCenterManagement1.slnx`.
Current result: 45/45 tests passed; build succeeded without warnings or errors.
Integration tests use the real login/JWT middleware, SQLite in-memory and mocked gateways, with no external payment calls.
SQL Server concurrency and live gateway acceptance still need environment-specific testing.
This branch preserves current Payment/PayOS/refund/idempotency behavior and does not add Reports or Check-in features.
