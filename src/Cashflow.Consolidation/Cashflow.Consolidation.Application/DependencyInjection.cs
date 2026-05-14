using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Cashflow.Consolidation.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddConsolidationApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        return services;
    }

    public static Assembly ApplicationAssembly => typeof(DependencyInjection).Assembly;
}
