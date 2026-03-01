using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace PaymentService.Api.Common;

public static class RateLimitingExtensions
{
    public static class PolicyNames
    {
        public const string Auth = "auth";
        public const string PaymentConfirm = "payment-confirm";
    }

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(limiterOptions =>
        {
            // Global limiter – applies to every endpoint; partitioned per user or IP
            limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var settings = context.RequestServices
                    .GetRequiredService<IOptions<RateLimitingSettings>>().Value;
                var key = BuildPartitionKey(context, "global");
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.Global.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.Global.WindowSeconds),
                    QueueLimit = settings.Global.QueueLimit,
                    AutoReplenishment = true
                });
            });

            // Auth policy – stricter limits for register/login/refresh to mitigate brute force
            limiterOptions.AddPolicy(PolicyNames.Auth, context =>
            {
                var settings = context.RequestServices
                    .GetRequiredService<IOptions<RateLimitingSettings>>().Value;
                var key = BuildPartitionKey(context, PolicyNames.Auth);
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.Auth.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.Auth.WindowSeconds),
                    QueueLimit = settings.Auth.QueueLimit,
                    AutoReplenishment = true
                });
            });

            // Payment-confirm policy – separate limit for the confirmation endpoint
            limiterOptions.AddPolicy(PolicyNames.PaymentConfirm, context =>
            {
                var settings = context.RequestServices
                    .GetRequiredService<IOptions<RateLimitingSettings>>().Value;
                var key = BuildPartitionKey(context, PolicyNames.PaymentConfirm);
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.PaymentConfirm.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.PaymentConfirm.WindowSeconds),
                    QueueLimit = settings.PaymentConfirm.QueueLimit,
                    AutoReplenishment = true
                });
            });

            // Rejection handler – 429 with Retry-After and ProblemDetails body
            limiterOptions.OnRejected = async (ctx, cancellationToken) =>
            {
                var httpContext = ctx.HttpContext;

                var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub");
                var ip = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                         ?? httpContext.Connection.RemoteIpAddress?.ToString()
                         ?? "unknown";
                var clientIdentifier = userId is not null ? $"user:{userId}" : $"ip:{ip}";

                var logger = httpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RateLimiting");

                logger.LogWarning(
                    "Rate limit exceeded. {Method} {Path} – Client: {Client}",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    clientIdentifier);

                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                httpContext.Response.ContentType = "application/problem+json";

                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    httpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();

                await httpContext.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc6585#section-4",
                    title = "Too Many Requests",
                    status = StatusCodes.Status429TooManyRequests,
                    detail = "Rate limit exceeded. Please try again later."
                }, cancellationToken: cancellationToken);
            };
        });

        return services;
    }

    // Prefer per-user limiting when authenticated, fall back to per-IP otherwise.
    private static string BuildPartitionKey(HttpContext context, string policyName)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? context.User.FindFirstValue("sub");

        if (!string.IsNullOrEmpty(userId))
            return $"{policyName}:user:{userId}";

        var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? context.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown";

        return $"{policyName}:ip:{ip}";
    }
}
