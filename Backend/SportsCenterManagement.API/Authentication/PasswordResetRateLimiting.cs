using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SportsCenterManagement.API.Authentication;

public static class PasswordResetRateLimiting
{
    public static void Configure(RateLimiterOptions options)
    {
        options.AddPolicy("password-reset", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
            }));
    }
}
