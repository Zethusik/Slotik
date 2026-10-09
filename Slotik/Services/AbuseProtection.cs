using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Slotik.Services;

public class BookingPolicy
{
    public int MaxPendingPerUser { get; set; } = 5;
    public int MaxAdvanceDays { get; set; } = 90;
    public int PendingLifetimeHours { get; set; } = 24;
    public DateTimeOffset PendingDeadline(DateTimeOffset now, DateTimeOffset startsAt) =>
        startsAt < now.AddHours(PendingLifetimeHours) ? startsAt : now.AddHours(PendingLifetimeHours);
}

public static class AbuseProtection
{
    public static IServiceCollection AddSlotikAbuseProtection(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BookingPolicy>().Bind(configuration.GetSection("BookingPolicy"))
            .Validate(o => o.MaxPendingPerUser is >= 1 and <= 100 && o.MaxAdvanceDays is >= 1 and <= 365
                && o.PendingLifetimeHours is >= 1 and <= 168, "Invalid booking policy.").ValidateOnStart();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retry.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
            Add("login", 10, 60, false); Add("recovery", 5, 900, false);
            Add("checkout", 5, 60, true); Add("uploads", 20, 60, true);
            Add("booking", 10, 60, true); Add("reconcile", 3, 60, true);

            void Add(string name, int permits, int seconds, bool useUser)
            {
                var limit = configuration.GetValue<int?>($"RateLimits:{name}:Permits") ?? permits;
                var window = configuration.GetValue<int?>($"RateLimits:{name}:WindowSeconds") ?? seconds;
                if (limit <= 0 || window <= 0) throw new InvalidOperationException("Rate limits must be positive.");
                options.AddPolicy(name, http => RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: useUser && http.User.UserId() is int id ? $"user:{id}" : $"ip:{http.Connection.RemoteIpAddress}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = limit, Window = TimeSpan.FromSeconds(window),
                        SegmentsPerWindow = 6, QueueLimit = 0, AutoReplenishment = true
                    }));
            }
        });
        return services;
    }
}
