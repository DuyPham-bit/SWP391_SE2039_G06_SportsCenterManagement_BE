using SportsCenterManagement.API.Extensions;
using SportsCenterManagement.BLL;

// DateTime values are stored as "timestamp without time zone" (same semantics as SQL Server datetime2).
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBusinessLogic(builder.Configuration.ResolveConnectionString());
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

// BE-004: Đồng bộ schema/quyền có kiểm soát môi trường và distributed application lock
await app.InitializeDatabaseAsync();
app.UseApiPipeline();

app.Run();
