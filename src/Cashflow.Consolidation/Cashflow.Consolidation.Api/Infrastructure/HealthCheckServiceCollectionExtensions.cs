namespace Cashflow.Consolidation.Api.Infrastructure;

internal static class HealthCheckServiceCollectionExtensions
{
    public static IServiceCollection AddConsolidationHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Mongo connection string é consumida pelo MongoContext via DI — aqui validamos
        // a presença para falhar cedo no boot.
        _ = configuration["Mongo:ConnectionString"]
            ?? throw new InvalidOperationException("Mongo:ConnectionString missing");
        var redisConn = configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:ConnectionString missing");
        var rabbitHost = configuration["RabbitMq:Host"] ?? "localhost";
        var rabbitPort = int.TryParse(configuration["RabbitMq:Port"], System.Globalization.CultureInfo.InvariantCulture, out var rp) ? rp : 5672;
        var keycloakAuthority = configuration["Keycloak:Authority"]
            ?? throw new InvalidOperationException("Keycloak:Authority missing");
        var keycloakMetadataAddress = configuration["Keycloak:MetadataAddress"];
        var keycloakHealthUrl = !string.IsNullOrWhiteSpace(keycloakMetadataAddress)
            ? keycloakMetadataAddress
            : $"{keycloakAuthority.TrimEnd('/')}/.well-known/openid-configuration";

        services.AddSingleton<MongoHealthCheck>();
        services.AddHealthChecks()
            .AddCheck<MongoHealthCheck>("mongo", tags: ["ready", "db"])
            .AddRedis(redisConn, name: "redis", tags: ["ready", "cache"])
            .AddCheck("rabbitmq", new RabbitMqHealthCheck(rabbitHost, rabbitPort), tags: ["ready", "broker"])
            .AddUrlGroup(new Uri(keycloakHealthUrl), name: "keycloak-discovery", tags: ["ready", "auth"]);

        return services;
    }
}
