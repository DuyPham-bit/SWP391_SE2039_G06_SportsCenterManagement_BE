# Database setup

The backend uses SQL Server and EF Core migrations. From the `Backend` directory, apply all migrations with:

```powershell
dotnet ef database update --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj
```

The API startup requires `Jwt__SecretKey` to be set to a private value of at least 32 UTF-8 bytes. Set it before running EF commands that load the API startup project; see `Backend/.env.example` and `Backend/README.md`.

`schema.sql` is an idempotent SQL Server script for the migrations. The Flow 3 migration adds payment idempotency/gateway references, cashier tendered amount, refund ledger, indexes, and status normalization. Review it before applying to a shared or production database.

To add a migration, run from `Backend`:

```powershell
dotnet ef migrations add DescribeChange --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj --output-dir Migrations
```
