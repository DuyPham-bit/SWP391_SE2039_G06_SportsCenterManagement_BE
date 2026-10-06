using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SportsCenterManagement.BLL.Common;
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

var jwtSecret = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException("Jwt:SecretKey must be configured with at least 32 UTF-8 bytes.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SportsCenterManagement";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SportsCenterManagement.Client";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name
        };
    });
builder.Services.AddAuthorization();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins);
    }
    else
    {
        policy.SetIsOriginAllowed(_ => false);
    }
    policy.AllowAnyHeader().AllowAnyMethod();
}));

// Layer 3: DAL (DbContext & Repository / Unit of Work Pattern)
builder.Services.AddDbContext<SportsCenterDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Layer 2: BLL (Business Logic Services)
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IMembershipPackageService, MembershipPackageService>();
builder.Services.AddScoped<ICoreFlowService, CoreFlowService>();
builder.Services.AddScoped<IReportService, ReportService>();

// >>> ĐĂNG KÝ CÁC SERVICE THANH TOÁN (VNPAY & MOMO) <<<
builder.Services.AddHttpClient<IVnPayService, VnPayService>();
builder.Services.AddHttpClient<IMoMoService, MoMoService>();
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
    await DbInitializer.SeedAsync(dbContext, builder.Environment.IsDevelopment());
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (BusinessException exception)
    {
        if (context.Response.HasStarted)
        {
            throw;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)exception.StatusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = exception.StatusCode.ToString(),
            status = (int)exception.StatusCode,
            detail = exception.Message,
            traceId = context.TraceIdentifier
        });
    }
    catch (Exception exception) when (
        exception.GetBaseException() is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
    {
        if (context.Response.HasStarted)
        {
            throw;
        }

        app.Logger.LogWarning(exception, "Unique constraint conflict. TraceId: {TraceId}", context.TraceIdentifier);
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = "Conflict",
            status = StatusCodes.Status409Conflict,
            detail = "Dữ liệu trùng với giao dịch đã ghi nhận. Kiểm tra trạng thái và gửi lại cùng Idempotency-Key nếu cần retry.",
            traceId = context.TraceIdentifier
        });
    }
    catch (Exception exception) when (
        exception.GetBaseException() is Microsoft.Data.SqlClient.SqlException { Number: 1205 })
    {
        if (context.Response.HasStarted)
        {
            throw;
        }

        app.Logger.LogWarning(exception, "Database transaction deadlock. TraceId: {TraceId}", context.TraceIdentifier);
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers["Retry-After"] = "1";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = "Service Unavailable",
            status = StatusCodes.Status503ServiceUnavailable,
            detail = "Giao dịch bị cạnh tranh trong cơ sở dữ liệu. Hãy retry với cùng Idempotency-Key.",
            traceId = context.TraceIdentifier
        });
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        // The client disconnected; there is no response to write.
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Unhandled request error. TraceId: {TraceId}", context.TraceIdentifier);
        if (context.Response.HasStarted)
        {
            throw;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = "Internal Server Error",
            status = StatusCodes.Status500InternalServerError,
            detail = "Đã xảy ra lỗi nội bộ. Hãy cung cấp traceId cho bộ phận hỗ trợ.",
            traceId = context.TraceIdentifier
        });
    }
});

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
