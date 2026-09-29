# Database setup

The EF Core model maps the SQL Server schema in the supplied ERD. Primary keys use `bigint`/`long`; table and column names, lengths, decimal precision, unique keys, and foreign keys are configured in the model.

## Create or update the local database

From this solution directory, restore the local EF tool and apply migrations:

```powershell
dotnet tool restore
dotnet ef database update --project .\SportsCenterManagement.Services\SportsCenterManagement.Services.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj
```

The default connection string targets SQL Server LocalDB and creates a database named `SportsCenterManagement`. To use another SQL Server, set the `ConnectionStrings__SportsCenter` environment variable before running the command. The `.env.example` file documents the expected value; .NET does not load `.env` files automatically.

## Add a schema change

```powershell
dotnet ef migrations add DescribeChange --project .\SportsCenterManagement.Services\SportsCenterManagement.Services.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj --output-dir Migrations
dotnet ef database update --project .\SportsCenterManagement.Services\SportsCenterManagement.Services.csproj --startup-project .\SportsCenterManagement.API\SportsCenterManagement.API.csproj
```

`schema.sql` is the idempotent SQL Server script generated from the migrations. It can be reviewed or applied with a SQL Server client after selecting the target database.
