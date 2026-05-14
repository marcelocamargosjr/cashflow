using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Cashflow.Ledger.Api.Authorization;

internal static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddLedgerAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var keycloakAuthority = configuration["Keycloak:Authority"]
            ?? throw new InvalidOperationException("Keycloak:Authority missing");
        var keycloakAudience = configuration["Keycloak:Audience"] ?? "cashflow-api";
        var requireHttpsMetadata = configuration.GetValue<bool?>("Keycloak:RequireHttpsMetadata") ?? !environment.IsDevelopment();
        // Quando rodando em container, o iss do JWT é http://localhost:8080/... (visto pelo
        // browser), mas a API precisa buscar JWKS pela DNS interna (http://keycloak:8080/...).
        // MetadataAddress sobrescreve a discovery URL sem mudar o ValidIssuer — §07 §3.1.1.
        var keycloakMetadataAddress = configuration["Keycloak:MetadataAddress"];

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloakAuthority;
                if (!string.IsNullOrWhiteSpace(keycloakMetadataAddress))
                    options.MetadataAddress = keycloakMetadataAddress;
                options.Audience = keycloakAudience;
                options.RequireHttpsMetadata = requireHttpsMetadata;
                // Sem o mapping, o claim "role" do Keycloak vira ClaimTypes.Role
                // e quebra IsInRole/RoleClaimType="role" — ver AuthorizationPolicies.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = keycloakAuthority,
                    ValidateAudience = false, // Keycloak coloca audience como cliente, validamos por role.
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "preferred_username",
                    RoleClaimType = "role"
                };
            });

        services.AddCashflowAuthorization();
        return services;
    }
}
