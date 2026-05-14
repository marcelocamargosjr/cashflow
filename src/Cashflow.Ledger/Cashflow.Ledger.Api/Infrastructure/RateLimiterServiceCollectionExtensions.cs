using System.Threading.RateLimiting;
using Cashflow.Ledger.Api.Authorization;

namespace Cashflow.Ledger.Api.Infrastructure;

internal static class RateLimiterServiceCollectionExtensions
{
    public static IServiceCollection AddLedgerRateLimiter(this IServiceCollection services)
    {
        services.AddRateLimiter(opt =>
        {
            opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opt.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                var key = http.GetMerchantId()?.ToString()
                    ?? http.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";
                return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromSeconds(1),
                    SegmentsPerWindow = 4,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            });
        });

        return services;
    }
}
