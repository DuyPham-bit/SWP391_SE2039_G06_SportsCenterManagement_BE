using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Threading.RateLimiting;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.API.BackgroundServices;
using SportsCenterManagement.API.Models;
using SportsCenterManagement.API.Services;
using SportsCenterManagement.API.HealthChecks;

namespace SportsCenterManagement.API.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Database connection string: ConnectionStrings:SportsCenter, DATABASE_URL-style variables or PG* parts;
    /// postgres:// URLs are converted to Npgsql format.
    /// </summary>
    public static string ResolveConnectionString(this IConfiguration configuration)
    {
        var rawConn = configuration.GetConnectionString("SportsCenter")
            ?? configuration["DATABASE_URL"]
            ?? configuration["DATABASE_PRIVATE_URL"]
            ?? configuration["POSTGRES_URL"]
            ?? configuration["PGURL"];

        if (string.IsNullOrWhiteSpace(rawConn))
        {
            var pgHost = configuration["PGHOST"] ?? configuration["POSTGRES_HOST"];
            var pgPort = configuration["PGPORT"] ?? configuration["POSTGRES_PORT"] ?? "5432";
            var pgDb = configuration["PGDATABASE"] ?? configuration["POSTGRES_DB"] ?? "railway";
            var pgUser = configuration["PGUSER"] ?? configuration["POSTGRES_USER"] ?? "postgres";
            var pgPass = configuration["PGPASSWORD"] ?? configuration["POSTGRES_PASSWORD"] ?? "";
            if (!string.IsNullOrWhiteSpace(pgHost))
            {
                rawConn = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPass}";
            }
        }

        if (string.IsNullOrWhiteSpace(rawConn))
        {
            throw new InvalidOperationException("Connection string is not configured. Set 'ConnectionStrings:SportsCenter' or 'DATABASE_URL'.");
        }

        string connectionString = rawConn;
        if (Uri.TryCreate(rawConn, UriKind.Absolute, out var uri) && (uri.Scheme == "postgres" || uri.Scheme == "postgresql"))
        {
            var userInfo = uri.UserInfo.Split(':');
            var builderConn = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres",
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
                Database = uri.AbsolutePath.TrimStart('/'),
                SslMode = Npgsql.SslMode.Prefer
            };
            connectionString = builderConn.ConnectionString;
        }

        return connectionString;
    }

    /// <summary>Authentication, authorization, CORS, rate limits, mail/storage, workers, controllers and Swagger.</summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtIssuer = new JwtTokenIssuer(configuration);

        // Cấu hình HttpClient để gọi API ngoài (MoMo Sandbox...)
        services.AddHttpClient();

        services.AddSingleton(jwtIssuer);
        services.AddScoped<JwtAccountValidator>();
        services.AddScoped<PasswordResetEmailSender>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = jwtIssuer.ValidationParameters();
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = BrowserSessionCookie.ReadAsync,
                OnTokenValidated = context => context.HttpContext.RequestServices
                    .GetRequiredService<JwtAccountValidator>().ValidateAsync(context)
            };
        });

        services.AddAuthorization(options => options.FallbackPolicy =
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (configuredOrigins.Length == 0)
        {
            var rawOrigins = configuration["Cors:AllowedOrigins"];
            if (!string.IsNullOrWhiteSpace(rawOrigins))
            {
                configuredOrigins = rawOrigins.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        }
        var allowedOrigins = configuredOrigins
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Where(origin => !string.IsNullOrEmpty(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (allowedOrigins.Count == 0)
        {
            allowedOrigins.Add("https://olympic-sports-arena.vercel.app");
            allowedOrigins.Add("http://localhost:3004");
            allowedOrigins.Add("http://localhost:5173");
        }
        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            policy.SetIsOriginAllowed(origin =>
                {
                    var normalized = origin.Trim().TrimEnd('/');
                    if (allowedOrigins.Contains(normalized))
                    {
                        return true;
                    }
                    if (Uri.TryCreate(normalized, UriKind.Absolute, out var originUri)
                        && string.Equals(originUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                        && originUri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)
                        && originUri.Host.StartsWith("olympic-sports-arena", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    return false;
                })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders("Retry-After");
        }));
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // Typed HTTP clients for BLL services (registered here: the BLL has no Microsoft.Extensions.Http).
        services.AddHttpClient<IVnPayService, VnPayService>();
        services.AddHttpClient<IMoMoService, MoMoService>();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            // Trust only the nearest hop (Railway/Vercel edge). Without this a client can append its own
            // X-Forwarded-For value and pick the IP that the per-IP rate limiters key on.
            options.ForwardLimit = 1;
        });
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddHostedService<ClassReminderWorker>();
        services.AddHostedService<CommunicationDeliveryWorker>();

        // MailKit SendMailService
        services.Configure<MailSettings>(configuration.GetSection("MailSettings"));
        services.AddTransient<ISendMailService, SendMailService>();

        services.Configure<R2StorageOptions>(configuration.GetSection(R2StorageOptions.SectionName));
        services.AddScoped<IR2StorageService, R2StorageService>();
        services.AddSingleton<GoogleTokenValidator>();
        services.AddHttpClient<GeminiService>(client => client.Timeout = TimeSpan.FromSeconds(45));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            PasswordResetRateLimiting.Configure(options);
            MailSendRateLimiting.Configure(options);
            // Per-IP cap on credential guessing; the per-account lockout alone does not stop horizontal
            // scans such as "login = password = phone" over many accounts.
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("ai", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("admin-management", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        // Layer 1: Presentation (API Controllers & Swagger UI)
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
                Description = "Nhập accessToken trả về từ POST /api/auth/login hoặc /api/auth/google."
            });
            options.OperationFilter<SportsCenterManagement.API.Authentication.JwtSecurityOperationFilter>();
        });

        return services;
    }
}
