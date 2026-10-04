using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Repositories.Implementations;
using SportsCenterManagement.DAL.Repositories.Interfaces;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("SportsCenter")
    ?? throw new InvalidOperationException(
        "Connection string 'SportsCenter' is not configured.");

// Cấu hình HttpClient để gọi API ngoài (MoMo Sandbox, PayOS...)
builder.Services.AddHttpClient();

// Layer 3: DAL (DbContext & Repository / Unit of Work Pattern)
builder.Services.AddDbContext<SportsCenterDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Layer 2: BLL (Business Logic Services)
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IMembershipPackageService, MembershipPackageService>();
builder.Services.AddScoped<ICoreFlowService, CoreFlowService>();

// >>> ĐĂNG KÝ CÁC SERVICE THANH TOÁN (VNPAY & MOMO) <<<
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IMoMoService, MoMoService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddHttpClient<IPayOsService, PayOsService>();


// Layer 1: Presentation (API Controllers & Swagger UI)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Khởi tạo Database và nạp dữ liệu mẫu (Packages, Members) nếu chưa có
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SportsCenterDbContext>();
    await DbInitializer.SeedAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
