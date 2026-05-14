using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Cashflow.Consolidation.Api.Authorization;

internal static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddConsolidationAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var keycloakAuthority = configuration["Keycloak:Authority"]
            ?? throw new InvalidOperationException("Keycloak:Authority missing");
        var keycloakAudience = configuration["Keycloak:Audience"] ?? "cashflow-api";
        var requireHttpsMetadata =
            configuration.GetValue<bool?>("Keycloak:RequireHttpsMetadata")
            ?? !environment.IsDevelopment();
        // Discovery URL interna para JWKS quando rodando em container — §07 §3.1.1.
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
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = keycloakAuthority,
                    ValidateAudience = false,
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
