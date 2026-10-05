using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Repositories.Implementations;
using SportsCenterManagement.DAL.Repositories.Interfaces;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("SportsCenter")
    ?? throw new InvalidOperationException(
        "Connection string 'SportsCenter' is not configured.");
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 256 bits.");
}

// Layer 3: DAL (DbContext & Repository / Unit of Work Pattern)
builder.Services.AddDbContext<SportsCenterDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Layer 2: BLL (Business Logic Services)
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IClassManagementService, ClassManagementService>();
builder.Services.AddScoped<IClassScheduleQueryService, ClassScheduleQueryService>();
builder.Services.AddScoped<ISessionBookingService, SessionBookingService>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();
builder.Services.AddScoped<IMembershipPackageService, MembershipPackageService>();
builder.Services.AddScoped<ICoreFlowService, CoreFlowService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://127.0.0.1:3003", "http://localhost:3003")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Layer 1: Presentation (API Controllers & Swagger UI)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Khởi tạo Database và nạp dữ liệu mẫu (Packages, Members) nếu chưa có
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SportsCenterDbContext>();
    var baselineLegacySchema = builder.Configuration.GetValue<bool>("Database:BaselineLegacySchema");
    await DbInitializer.SeedAsync(dbContext, baselineLegacySchema);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();


