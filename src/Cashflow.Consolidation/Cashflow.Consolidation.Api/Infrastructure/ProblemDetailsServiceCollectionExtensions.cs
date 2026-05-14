using Cashflow.SharedKernel.Http;

namespace Cashflow.Consolidation.Api.Infrastructure;

internal static class ProblemDetailsServiceCollectionExtensions
{
    public static IServiceCollection AddConsolidationProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                if (ctx.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.HttpContextItemKey, out var corr)
                    && corr is string correlation)
                {
                    ctx.ProblemDetails.Extensions["correlationId"] = correlation;
                }
            };
        });
        services.AddTransient<ExceptionToProblemDetailsMiddleware>();
        return services;
    }
}
