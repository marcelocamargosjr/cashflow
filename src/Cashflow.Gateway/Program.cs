using System.Security.Claims;
using System.Threading.RateLimiting;
using Cashflow.SharedKernel.Http;
using Cashflow.SharedKernel.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Transforms;

const string BalanceReadPolicy = "balance-read-policy";
const string EntryWritePolicy = "entry-write-policy";
const string RequireMerchantPolicy = "RequireMerchant";
const string RequireAdminPolicy = "RequireAdmin";
const string MerchantIdClaim = "merchantId";

var builder = WebApplication.CreateBuilder(args);

builder.AddCashflowObservability("cashflow.gateway", "1.0.0");

// ===== Authentication / Authorization (mesma config das APIs — §07 §3.2) =====
var keycloakAuthority = builder.Configuration["Keycloak:Authority"]
    ?? throw new InvalidOperationException("Keycloak:Authority missing");
var keycloakAudience = builder.Configuration["Keycloak:Audience"] ?? "cashflow-api";
var requireHttpsMetadata = builder.Configuration.GetValue<bool?>("Keycloak:RequireHttpsMetadata")
    ?? !builder.Environment.IsDevelopment();
// Discovery interna (DNS de container) sem mudar o ValidIssuer — §07 §3.1.1.
var keycloakMetadataAddress = builder.Configuration["Keycloak:MetadataAddress"];

builder.Services
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(RequireMerchantPolicy, policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(ctx => HasRealmRole(ctx.User, "merchant") || HasRealmRole(ctx.User, "admin")));

    options.AddPolicy(RequireAdminPolicy, policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(ctx => HasRealmRole(ctx.User, "admin")));
});

// ===== Rate limiter (§07 §3.3 — headroom 2× sobre o NFR de 50 req/s) =====
// balance-read-policy: 120/s/merchant. entry-write-policy: 200/s/merchant.
// Particionado pelo claim `merchantId` para isolar consumidores e absorver bursts.
builder.Services.AddRateLimiter(opt =>
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

// ===== HealthChecks (Keycloak discovery — gateway não toca DB direto) =====
var keycloakHealthUrl = !string.IsNullOrWhiteSpace(keycloakMetadataAddress)
    ? keycloakMetadataAddress
    : $"{keycloakAuthority.TrimEnd('/')}/.well-known/openid-configuration";
builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri(keycloakHealthUrl), name: "keycloak-discovery", tags: new[] { "ready", "auth" });

// ===== Problem Details =====
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

// ===== YARP =====
// Transforms: garante X-Correlation-Id (gera se ausente) e propaga `traceparent`
// para que o trace do cliente flua até o backend (W3C Trace Context).
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformContext =>
    {
        transformContext.AddRequestTransform(ctx =>
        {
            // X-Correlation-Id já é resolvido pelo CorrelationIdMiddleware (gera se ausente)
            // e fica em HttpContext.Items. Aqui apenas propaga ao destino.
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

var app = builder.Build();

app.UseCashflowCorrelationId();
app.UseStatusCodePages();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
// /health = liveness simples (compat com healthcheck do compose).
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });

app.MapReverseProxy();

await app.RunAsync().ConfigureAwait(false);

static string ResolvePartitionKey(HttpContext ctx)
{
    var merchantId = ctx.User.FindFirstValue(MerchantIdClaim);
    if (!string.IsNullOrWhiteSpace(merchantId))
        return merchantId;

    var sub = ctx.User.FindFirstValue("sub");
    if (!string.IsNullOrWhiteSpace(sub))
        return sub;

    return ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
}

static bool HasRealmRole(ClaimsPrincipal user, string role)
{
    return user.IsInRole(role)
        || user.HasClaim(c => string.Equals(c.Type, "role", StringComparison.Ordinal) && string.Equals(c.Value, role, StringComparison.Ordinal))
        || user.HasClaim(c => string.Equals(c.Type, "roles", StringComparison.Ordinal) && string.Equals(c.Value, role, StringComparison.Ordinal));
}

public partial class Program;
