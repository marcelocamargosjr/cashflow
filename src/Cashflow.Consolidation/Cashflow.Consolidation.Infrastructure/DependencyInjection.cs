using Cashflow.Consolidation.Application.Abstractions;
using Cashflow.Consolidation.Infrastructure.Caching;
using Cashflow.Consolidation.Infrastructure.Persistence;
using Cashflow.Consolidation.Infrastructure.Projections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using StackExchange.Redis;

namespace Cashflow.Consolidation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddConsolidationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));

        services.AddSingleton<IMongoClient>(sp =>
        {
            var options = configuration.GetSection(MongoOptions.SectionName).Get<MongoOptions>()
                ?? throw new InvalidOperationException("Mongo configuration section missing");
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                throw new InvalidOperationException("Mongo:ConnectionString missing");
            return new MongoClient(options.ConnectionString);
        });

        services.AddSingleton<MongoContext>();

        services.AddScoped<IProjectionService, ProjectionService>();
        services.AddScoped<IDailyBalanceReadRepository, DailyBalanceReadRepository>();

        return services;
    }

    public static IServiceCollection AddConsolidationCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()
                ?? throw new InvalidOperationException("Redis configuration section missing");
            return ConnectionMultiplexer.Connect(options.ConnectionString);
        });

        services.AddSingleton<IDailyBalanceCache, RedisDailyBalanceCache>();
        return services;
    }
}
