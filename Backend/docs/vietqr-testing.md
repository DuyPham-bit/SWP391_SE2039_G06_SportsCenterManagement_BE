# VietQR / PayOS Testing

## Branch Contract

Create checkout with `POST /api/payments/create-payos-url`, a member JWT and an
`Idempotency-Key` header. The response contains `paymentUrl`.
This branch maps the PayOS `orderCode` to `Payment.Id` / `GatewayReference`,
not `Invoice.Id`. Keep this mapping when processing existing orders.

## Recovery After a Missed Webhook

Log in as the member who owns the payment attempt, then call:

```http
POST /api/payments/vietqr/{orderCode}/sync
Authorization: Bearer <accessToken>
```

No request body is required. The backend queries PayOS directly and checks the
original response signature, order code, amount paid and bank references.
Only a matching fully paid attempt is completed. The existing completion
transaction preserves duplicate protection, review states and refund handling.
Repeated synchronization does not create another payment or extend membership
again. A late payment after a void is recorded for manager review/refund and
does not reactivate the cancelled membership.

Read `success` and `message` in the response. Unpaid orders remain pending;
another member's payment returns HTTP 403; missing authentication returns 401.
A provider failure or unverifiable response returns 503 without recording a
successful payment. Do not pay again just because synchronization failed.

## Public Webhook: Deferred

Public webhook configuration and automatic end-to-end testing are paused at
the user's request on 2026-10-13. Resume only when requested.

The backend accepts both `/api/payments/payos-ipn` and
`/api/payments/payos-webhook`. Future setup requires a public HTTPS URL that
forwards to the running API and is registered on the correct PayOS channel.
`ReturnUrl` and `CancelUrl` navigate the browser; they do not update the invoice
by themselves. Frontend integration of the sync endpoint is still pending.

## Credentials and Tests

Keep real `PayOS:ClientId`, `PayOS:ApiKey` and `PayOS:ChecksumKey` in environment
variables or User Secrets. Repository configuration contains placeholders.

```powershell
dotnet test Backend/SportsCenterManagement.Tests/SportsCenterManagement.Tests.csproj
```

The automated tests use fake HTTP responses and an isolated SQLite database;
they do not transfer money. Real checkout uses real bank transfers.
