namespace Cashflow.Ledger.Api.Infrastructure;

internal static class HealthCheckServiceCollectionExtensions
{
    public static IServiceCollection AddLedgerHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var postgresConn = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres missing");
        var rabbitHost = configuration["RabbitMq:Host"] ?? "localhost";
        var rabbitPort = int.TryParse(configuration["RabbitMq:Port"], System.Globalization.CultureInfo.InvariantCulture, out var rp) ? rp : 5672;
        var keycloakAuthority = configuration["Keycloak:Authority"]
            ?? throw new InvalidOperationException("Keycloak:Authority missing");
        var keycloakMetadataAddress = configuration["Keycloak:MetadataAddress"];

        // Health check usa a discovery URL interna (mesma que o JwtBearer) para que o probe
        // passe mesmo quando o `localhost` do iss não é roteável de dentro do container.
        var keycloakHealthUrl = !string.IsNullOrWhiteSpace(keycloakMetadataAddress)
            ? keycloakMetadataAddress
            : $"{keycloakAuthority.TrimEnd('/')}/.well-known/openid-configuration";

        services.AddHealthChecks()
            .AddNpgSql(postgresConn, name: "postgres", tags: ["ready", "db"])
            .AddCheck("rabbitmq", new RabbitMqHealthCheck(rabbitHost, rabbitPort), tags: ["ready", "broker"])
            .AddUrlGroup(new Uri(keycloakHealthUrl), name: "keycloak-discovery", tags: ["ready", "auth"]);

        return services;
    }
}
