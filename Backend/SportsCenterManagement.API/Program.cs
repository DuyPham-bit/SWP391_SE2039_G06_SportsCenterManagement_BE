using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Repositories.Implementations;
using SportsCenterManagement.DAL.Repositories.Interfaces;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("SportsCenter")
    ?? throw new InvalidOperationException("Connection string 'SportsCenter' is not configured.");
var jwtIssuer = new JwtTokenIssuer(builder.Configuration);

// Cấu hình HttpClient để gọi API ngoài (MoMo Sandbox, PayOS...)
builder.Services.AddHttpClient();

// Layer 3: DAL (DbContext & Repository / Unit of Work Pattern)
builder.Services.AddDbContext<SportsCenterDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddSingleton(jwtIssuer);
builder.Services.AddScoped<JwtAccountValidator>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = jwtIssuer.ValidationParameters();
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context => context.HttpContext.RequestServices
            .GetRequiredService<JwtAccountValidator>().ValidateAsync(context)
    };
});

builder.Services.AddAuthorization(options => options.FallbackPolicy =
    new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Retry-After");
}));
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Layer 2: BLL (Business Logic Services)
// Flow 1 Services
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IMembershipPackageService, MembershipPackageService>();
builder.Services.AddScoped<ICoreFlowService, CoreFlowService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMemberService, MemberService>();
builder.Services.AddScoped<IAccessControlService, AccessControlService>();
builder.Services.AddScoped<IStaffManagementService, StaffManagementService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// Flow 2 Services
builder.Services.AddScoped<IClassManagementService, ClassManagementService>();
builder.Services.AddScoped<IClassScheduleQueryService, ClassScheduleQueryService>();
builder.Services.AddScoped<ISessionBookingService, SessionBookingService>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();

// Flow 3 Services
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddHttpClient<IVnPayService, VnPayService>();
builder.Services.AddHttpClient<IMoMoService, MoMoService>();
builder.Services.AddHttpClient<IPayOsService, PayOsService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ICheckinService, CheckinService>();
builder.Services.AddScoped<SupportRequestService>();
builder.Services.AddScoped<TrainingService>();
builder.Services.AddSingleton<GoogleTokenValidator>();
builder.Services.AddHttpClient<GeminiService>(client => client.Timeout = TimeSpan.FromSeconds(45));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ai", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Layer 1: Presentation (API Controllers & Swagger UI)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Nhập accessToken trả về từ POST /api/auth/login hoặc /api/auth/google."
    });
    options.OperationFilter<SportsCenterManagement.API.Authentication.JwtSecurityOperationFilter>();
});

var app = builder.Build();

// Khởi tạo Database và nạp dữ liệu mẫu nếu chưa có
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SportsCenterDbContext>();
    var baselineLegacySchema = app.Configuration.GetValue<bool>("Database:BaselineLegacySchema");
    await DbInitializer.SeedAsync(dbContext, baselineLegacySchema, app.Environment.IsDevelopment());
    await SystemAdminBootstrapper.SeedAsync(dbContext, app.Configuration);
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

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.Use(async (context, next) =>
{
    var isPaymentCreateRequest = context.Request.Method == HttpMethods.Post &&
        context.Request.Path.Value is "/api/payments/create-vnpay-url"
            or "/api/payments/create-momo-url"
            or "/api/payments/create-payos-url";

    if (isPaymentCreateRequest)
    {
        app.Logger.LogInformation(
            "Payment debug: request reached backend. Path={Path} TraceId={TraceId}",
            context.Request.Path,
            context.TraceIdentifier);
    }

    try
    {
        await next();
    }
    finally
    {
        if (isPaymentCreateRequest)
        {
            app.Logger.LogInformation(
                "Payment debug: backend response sent. Path={Path} StatusCode={StatusCode} TraceId={TraceId}",
                context.Request.Path,
                context.Response.StatusCode,
                context.TraceIdentifier);
        }
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/payment-result", async context =>
{
    var frontendIndex = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html");
    if (!File.Exists(frontendIndex))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(frontendIndex);
}).AllowAnonymous();
app.Run();
