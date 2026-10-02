# Database setup

The EF Core model is the source of truth for the SQL Server schema. The idempotent `schema.sql` script is generated from migrations. Primary keys use `bigint`/`long`; lengths, decimal precision, foreign keys, filtered unique indexes, and business check constraints are defined in the model.

## Create or update the local database

Run these commands from the `Backend` directory. Migration hardening adds refund storage, idempotency, audit/report scope, class-wide plans, and database constraints. Apply migrations to a disposable/local database first; legacy rows that violate a new unique/check constraint must be reviewed and corrected before deployment. The migration does not delete duplicate business history automatically.

From this solution directory, restore the local EF tool and apply migrations:

```powershell
dotnet tool restore
dotnet ef database update --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj
```

The default connection string targets SQL Server LocalDB and creates a database named `SportsCenterManagement`. To use another SQL Server, set the `ConnectionStrings__SportsCenter` environment variable before running the command. The `.env.example` file documents the expected value; .NET does not load `.env` files automatically.

## Add a schema change

```powershell
dotnet ef migrations add DescribeChange --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj --output-dir Migrations
dotnet ef database update --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj
```

Regenerate the deployable SQL Server script after adding a migration:

```powershell
dotnet ef migrations script --idempotent --project .\SportsCenterManagement.DAL\SportsCenterManagement.DAL.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj --output .\database\schema.sql
```

`schema.sql` is the idempotent SQL Server script generated from the migrations. It can be reviewed or applied with a SQL Server client after selecting the target database. It is not run automatically by the API.
