using Microsoft.OpenApi.Models;

namespace Cashflow.Consolidation.Api.Infrastructure;

internal static class SwaggerServiceCollectionExtensions
{
    public static IServiceCollection AddConsolidationSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Cashflow Consolidation API",
                Version = "v1",
                Description = "Read-side API para o saldo diário consolidado."
            });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT emitido pelo Keycloak (realm cashflow)."
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }] = []
            });
        });

        return services;
    }
}
