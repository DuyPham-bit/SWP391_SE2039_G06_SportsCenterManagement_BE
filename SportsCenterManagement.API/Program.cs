using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.Services;
using SportsCenterManagement.Services.Features.CoreFlows;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("SportsCenter")
    ?? throw new InvalidOperationException(
        "Connection string 'SportsCenter' is not configured.");

builder.Services.AddDbContext<SportsCenterDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddScoped<CoreFlowService>();
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
