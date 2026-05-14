using System.Security.Claims;
using System.Threading.RateLimiting;
using Cashflow.SharedKernel.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Transforms;

namespace Cashflow.Gateway;

internal static class GatewayServiceCollectionExtensions
{
    public const string BalanceReadPolicy = "balance-read-policy";
    public const string EntryWritePolicy = "entry-write-policy";
    public const string RequireMerchantPolicy = "RequireMerchant";
    public const string RequireAdminPolicy = "RequireAdmin";
    public const string MerchantIdClaim = "merchantId";

    public static IServiceCollection AddGatewayAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var keycloakAuthority = configuration["Keycloak:Authority"]
            ?? throw new InvalidOperationException("Keycloak:Authority missing");
        var keycloakAudience = configuration["Keycloak:Audience"] ?? "cashflow-api";
        var requireHttpsMetadata = configuration.GetValue<bool?>("Keycloak:RequireHttpsMetadata")
            ?? !environment.IsDevelopment();
        // Discovery interna (DNS de container) sem mudar o ValidIssuer — §07 §3.1.1.
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

        services.AddAuthorization(options =>
        {
            options.AddPolicy(RequireMerchantPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => HasRealmRole(ctx.User, "merchant") || HasRealmRole(ctx.User, "admin")));

            options.AddPolicy(RequireAdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => HasRealmRole(ctx.User, "admin")));
        });

        return services;
    }

    public static IServiceCollection AddGatewayRateLimiter(this IServiceCollection services)
    {
        // §07 §3.3 — headroom 2× sobre o NFR de 50 req/s.
        // balance-read-policy: 120/s/merchant. entry-write-policy: 200/s/merchant.
        services.AddRateLimiter(opt =>
        {
            opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            opt.AddPolicy(BalanceReadPolicy, ctx => RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: ResolvePartitionKey(ctx),
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromSeconds(1),
                    SegmentsPerWindow = 10,
                    QueueLimit = 50,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                }));

            opt.AddPolicy(EntryWritePolicy, ctx => RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: ResolvePartitionKey(ctx),
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 200,
                    Window = TimeSpan.FromSeconds(1),
                    SegmentsPerWindow = 10,
                    QueueLimit = 50,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                }));

            opt.OnRejected = async (ctx, cancellationToken) =>
            {
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                ctx.HttpContext.Response.Headers.RetryAfter = "1";
                await ctx.HttpContext.Response.WriteAsJsonAsync(
                    ProblemDetailsExtensions.RateLimit("rate limit exceeded", ctx.HttpContext),
                    cancellationToken).ConfigureAwait(false);
            };
        });

        return services;
    }

    public static IServiceCollection AddGatewayHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var keycloakAuthority = configuration["Keycloak:Authority"]
            ?? throw new InvalidOperationException("Keycloak:Authority missing");
        var keycloakMetadataAddress = configuration["Keycloak:MetadataAddress"];
        var keycloakHealthUrl = !string.IsNullOrWhiteSpace(keycloakMetadataAddress)
            ? keycloakMetadataAddress
            : $"{keycloakAuthority.TrimEnd('/')}/.well-known/openid-configuration";

        services.AddHealthChecks()
            .AddUrlGroup(new Uri(keycloakHealthUrl), name: "keycloak-discovery", tags: ["ready", "auth"]);

        return services;
    }

    public static IServiceCollection AddGatewayProblemDetails(this IServiceCollection services)
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

        return services;
    }

    public static IServiceCollection AddGatewayReverseProxy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Transforms: garante X-Correlation-Id (gera se ausente) e propaga `traceparent`
        // para que o trace do cliente flua até o backend (W3C Trace Context).
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms(transformContext =>
            {
                transformContext.AddRequestTransform(ctx =>
                {
                    if (ctx.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.HttpContextItemKey, out var corr)
                        && corr is string correlation)
                    {
                        ctx.ProxyRequest.Headers.Remove(CorrelationIdMiddleware.HeaderName);
                        ctx.ProxyRequest.Headers.TryAddWithoutValidation(
                            CorrelationIdMiddleware.HeaderName, correlation);
                    }

                    // W3C Trace Context — preserve traceparent vindo do cliente quando presente.
                    // Quando ausente, deixa o ASP.NET/OTel injetar o do Activity atual (default).
                    if (ctx.HttpContext.Request.Headers.TryGetValue("traceparent", out var traceparent)
                        && !string.IsNullOrWhiteSpace(traceparent))
                    {
                        ctx.ProxyRequest.Headers.Remove("traceparent");
                        ctx.ProxyRequest.Headers.TryAddWithoutValidation("traceparent", traceparent.ToString());
                    }

                    if (ctx.HttpContext.Request.Headers.TryGetValue("tracestate", out var tracestate)
                        && !string.IsNullOrWhiteSpace(tracestate))
                    {
                        ctx.ProxyRequest.Headers.Remove("tracestate");
                        ctx.ProxyRequest.Headers.TryAddWithoutValidation("tracestate", tracestate.ToString());
                    }

                    return ValueTask.CompletedTask;
                });
            });

        return services;
    }

    internal static string ResolvePartitionKey(HttpContext ctx)
    {
        var merchantId = ctx.User.FindFirstValue(MerchantIdClaim);
        if (!string.IsNullOrWhiteSpace(merchantId))
            return merchantId;

        var sub = ctx.User.FindFirstValue("sub");
        if (!string.IsNullOrWhiteSpace(sub))
            return sub;

        return ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
    }

    internal static bool HasRealmRole(ClaimsPrincipal user, string role)
    {
        return user.IsInRole(role)
            || user.HasClaim(c => string.Equals(c.Type, "role", StringComparison.Ordinal) && string.Equals(c.Value, role, StringComparison.Ordinal))
            || user.HasClaim(c => string.Equals(c.Type, "roles", StringComparison.Ordinal) && string.Equals(c.Value, role, StringComparison.Ordinal));
    }
}
