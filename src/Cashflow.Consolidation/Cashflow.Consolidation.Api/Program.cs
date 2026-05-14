using Cashflow.Consolidation.Api.Authorization;
using Cashflow.Consolidation.Api.Endpoints;
using Cashflow.Consolidation.Api.Infrastructure;
using Cashflow.Consolidation.Application;
using Cashflow.Consolidation.Infrastructure;
using Cashflow.Consolidation.Infrastructure.Caching;
using Cashflow.Consolidation.Infrastructure.Persistence;
using Cashflow.SharedKernel.Http;
using Cashflow.SharedKernel.Observability;
using Cashflow.SharedKernel.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddCashflowObservability("cashflow.consolidation.api", "1.0.0");

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddConsolidationApplication();
builder.Services.AddConsolidationInfrastructure(builder.Configuration);
builder.Services.AddConsolidationCache(builder.Configuration);

// ====== Authentication / Authorization ======
var keycloakAuthority = builder.Configuration["Keycloak:Authority"]
    ?? throw new InvalidOperationException("Keycloak:Authority missing");
var keycloakAudience = builder.Configuration["Keycloak:Audience"] ?? "cashflow-api";
var requireHttpsMetadata =
    builder.Configuration.GetValue<bool?>("Keycloak:RequireHttpsMetadata")
    ?? !builder.Environment.IsDevelopment();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakAuthority;
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

builder.Services.AddCashflowAuthorization();

// ====== HealthChecks (Mongo + Redis + Keycloak JWKS + RabbitMQ socket) ======
var mongoConn = builder.Configuration["Mongo:ConnectionString"]
    ?? throw new InvalidOperationException("Mongo:ConnectionString missing");
var redisConn = builder.Configuration["Redis:ConnectionString"]
    ?? throw new InvalidOperationException("Redis:ConnectionString missing");
var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitPort = int.TryParse(builder.Configuration["RabbitMq:Port"], out var rp) ? rp : 5672;
var keycloakJwksUrl = $"{keycloakAuthority.TrimEnd('/')}/protocol/openid-connect/certs";

builder.Services.AddHealthChecks()
    .AddMongoDb(
        sp => new MongoDB.Driver.MongoClient(mongoConn),
        name: "mongo",
        tags: new[] { "ready", "db" })
    .AddRedis(redisConn, name: "redis", tags: new[] { "ready", "cache" })
    .AddCheck("rabbitmq", new RabbitMqHealthCheck(rabbitHost, rabbitPort), tags: new[] { "ready", "broker" })
    .AddUrlGroup(new Uri(keycloakJwksUrl), name: "keycloak-jwks", tags: new[] { "ready", "auth" });

// ====== Problem Details ======
builder.Services.AddProblemDetails(options =>
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
builder.Services.AddTransient<ExceptionToProblemDetailsMiddleware>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Cashflow Consolidation API",
        Version = "v1",
        Description = "Read-side API para o saldo diário consolidado."
    });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT emitido pelo Keycloak (realm cashflow)."
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Reference = new Microsoft.OpenApi.Models.OpenApiReference
            {
                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        }] = Array.Empty<string>()
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionToProblemDetailsMiddleware>();

app.UseCashflowCorrelationId();
app.UseStatusCodePages();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapBalancesEndpoints();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthChecksResponseWriter.WriteAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthChecksResponseWriter.WriteAsync
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapScalarApiReference(o => o.Title = "Cashflow Consolidation");
}

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
