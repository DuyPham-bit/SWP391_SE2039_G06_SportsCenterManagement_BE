using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.StaticFiles;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Seed;

namespace SportsCenterManagement.API.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>Creates/seeds the database under a distributed lock when Database:AutoMigrateOnStartup is on.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        // Production deployments must run migrations as a separate, reviewed release step.
        var autoMigrateOnStartup = app.Configuration.GetValue<bool>("Database:AutoMigrateOnStartup", false);
        if (autoMigrateOnStartup)
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SportsCenterDbContext>();
            var databaseCreator = dbContext.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
            if (!await databaseCreator.ExistsAsync()) await databaseCreator.CreateAsync();

            await using var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var lockCommand = connection.CreateCommand();
            lockCommand.CommandText = "SET lock_timeout = '60s'; SELECT pg_advisory_lock(hashtext('SCMS_Database_Migration_Lock')); RESET lock_timeout;";
            await lockCommand.ExecuteNonQueryAsync();

            try
            {
                await DbInitializer.SeedAsync(dbContext, app.Configuration.GetValue<bool>("Database:SeedDemoData", false));
                await AdminBootstrapper.SeedAsync(dbContext, app.Configuration);
                await LandingContentService.EnsureSeededAsync(dbContext);
            }
            finally
            {
                await using var releaseCommand = connection.CreateCommand();
                releaseCommand.CommandText = "SELECT pg_advisory_unlock(hashtext('SCMS_Database_Migration_Lock'))";
                try { await releaseCommand.ExecuteNonQueryAsync(); } catch { }
            }
        }
    }

    /// <summary>Middleware pipeline and endpoints, in the original order.</summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors();
        // Must wrap the exception handlers below so their error bodies are localized too.
        app.UseMiddleware<SportsCenterManagement.API.Localization.ResponseMessageLocalizationMiddleware>();

        static void ClearResponsePreservingCors(HttpResponse response)
        {
            var corsHeaders = response.Headers
                .Where(h => h.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(h.Key, "Vary", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            response.Clear();
            foreach (var header in corsHeaders)
            {
                response.Headers[header.Key] = header.Value;
            }
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

                ClearResponsePreservingCors(context.Response);
                context.Response.StatusCode = (int)exception.StatusCode;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "about:blank",
                    title = exception.StatusCode.ToString(),
                    status = (int)exception.StatusCode,
                    code = exception.Code,
                    detail = exception.Message,
                    message = exception.Message,
                    proposedHandling = exception.ProposedHandling,
                    isDraftPolicy = exception.IsDraftPolicy,
                    traceId = context.TraceIdentifier
                });
            }
            catch (Exception exception) when (
                exception.GetBaseException() is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
            {
                if (context.Response.HasStarted)
                {
                    throw;
                }

                app.Logger.LogWarning(exception, "Unique constraint conflict. TraceId: {TraceId}", context.TraceIdentifier);
                ClearResponsePreservingCors(context.Response);
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "about:blank",
                    title = "Conflict",
                    status = StatusCodes.Status409Conflict,
                    code = "COMMON-03",
                    detail = "Dữ liệu trùng với giao dịch đã ghi nhận hoặc vừa bị thay đổi. Kiểm tra trạng thái và tải lại nếu cần.",
                    message = "Dữ liệu trùng với giao dịch đã ghi nhận hoặc vừa bị thay đổi.",
                    proposedHandling = "Phát hiện dữ liệu đã thay đổi; yêu cầu tải bản mới để đối chiếu, tránh âm thầm ghi đè.",
                    traceId = context.TraceIdentifier
                });
            }
            catch (Exception exception) when (
                exception.GetBaseException() is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.DeadlockDetected or Npgsql.PostgresErrorCodes.SerializationFailure })
            {
                if (context.Response.HasStarted)
                {
                    throw;
                }

                app.Logger.LogWarning(exception, "Database transaction deadlock. TraceId: {TraceId}", context.TraceIdentifier);
                ClearResponsePreservingCors(context.Response);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/problem+json";
                context.Response.Headers["Retry-After"] = "1";
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "about:blank",
                    title = "Service Unavailable",
                    status = StatusCodes.Status503ServiceUnavailable,
                    code = "COMMON-01",
                    detail = "Giao dịch bị cạnh tranh trong cơ sở dữ liệu. Hãy retry với cùng Idempotency-Key.",
                    message = "Chưa xác định kết quả; kiểm tra trạng thái trước khi thử lại.",
                    proposedHandling = "Chưa xác định kết quả; kiểm tra trạng thái trước khi thử lại. Tránh tạo hai đăng ký hoặc hai khoản thu.",
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

                ClearResponsePreservingCors(context.Response);
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

        var mediaContentTypes = new FileExtensionContentTypeProvider();
        mediaContentTypes.Mappings[".avif"] = "image/avif";
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = mediaContentTypes,
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";

                // The HTML document is the manifest for Vite's content-hashed chunks. It
                // must be revalidated after a deployment so it never points at deleted chunks.
                if (string.Equals(context.File.Name, "index.html", StringComparison.OrdinalIgnoreCase))
                {
                    context.Context.Response.Headers.CacheControl = "no-cache, no-store, max-age=0";
                }
            }
        });
        app.Use(async (context, next) =>
        {
            await next();

            // A browser can keep an old entry bundle while a new frontend build has removed
            // its hashed lazy chunk. Return a one-shot ES module that reloads the current
            // deployment instead of leaving the SPA broken on a permanent 404.
            var isMissingJavaScriptChunk = context.Response.StatusCode == StatusCodes.Status404NotFound &&
                !context.Response.HasStarted &&
                HttpMethods.IsGet(context.Request.Method) &&
                context.Request.Path.StartsWithSegments("/assets") &&
                string.Equals(Path.GetExtension(context.Request.Path.Value), ".js", StringComparison.OrdinalIgnoreCase);

            if (isMissingJavaScriptChunk)
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/javascript; charset=utf-8";
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsync("window.location.replace('/');");
            }
        });
        app.UseRouting();
        app.Use(async (context, next) =>
        {
            var isPaymentCreateRequest = context.Request.Method == HttpMethods.Post &&
                context.Request.Path.Value is "/api/payments/create-vnpay-url"
                    or "/api/payments/create-momo-url";

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

        // BE-007: Health Check Endpoints
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        }).AllowAnonymous();

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        }).AllowAnonymous();

        app.MapGet("/payment-result", async context =>
        {
            // Gateway may return to the API host; bounce to the configured SPA so the member lands back in the app.
            if (Uri.TryCreate(app.Configuration["Frontend:BaseUrl"]?.Trim(), UriKind.Absolute, out var frontendBase)
                && (frontendBase.Scheme == Uri.UriSchemeHttps || frontendBase.Scheme == Uri.UriSchemeHttp)
                && !string.Equals(frontendBase.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase))
            {
                var target = frontendBase.GetLeftPart(UriPartial.Authority) + frontendBase.AbsolutePath.TrimEnd('/')
                    + "/payment-result" + context.Request.QueryString.Value;
                context.Response.Redirect(target);
                return;
            }

            var frontendIndex = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html");
            if (!File.Exists(frontendIndex))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(frontendIndex);
        }).AllowAnonymous();

        return app;
    }
}
