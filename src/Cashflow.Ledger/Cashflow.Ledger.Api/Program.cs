using System.Threading.RateLimiting;
using Cashflow.Ledger.Api.Authorization;
using Cashflow.Ledger.Api.Endpoints;
using Cashflow.Ledger.Api.Infrastructure;
using Cashflow.Ledger.Application;
using Cashflow.Ledger.Infrastructure;
using Cashflow.Ledger.Infrastructure.Persistence;
using Cashflow.SharedKernel.Http;
using Cashflow.SharedKernel.Observability;
using Cashflow.SharedKernel.Time;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

Console.WriteLine("[BOOT] entering Program.Main");
Console.Out.Flush();

var builder = WebApplication.CreateBuilder(args);
Console.WriteLine("[BOOT] CreateBuilder done");
Console.Out.Flush();

builder.AddCashflowObservability("cashflow.ledger.api", "1.0.0");
Console.WriteLine("[BOOT] observability registered");
Console.Out.Flush();

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddLedgerApplication();
builder.Services.AddLedgerInfrastructure(builder.Configuration);

// ====== MassTransit + Outbox transacional (06 §2.2) ======
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    x.AddEntityFrameworkOutbox<LedgerDbContext>(o =>
    {
        o.QueryDelay = TimeSpan.FromSeconds(1);
        o.UsePostgres();
        o.UseBusOutbox();
        o.DuplicateDetectionWindow = TimeSpan.FromHours(24);
    });

    x.UsingRabbitMq((ctx, cfg) =>
    {
        var host = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        var vhost = builder.Configuration["RabbitMq:VirtualHost"] ?? "/";
        var user = builder.Configuration["RabbitMq:Username"] ?? "cashflow";
        var pwd = builder.Configuration["RabbitMq:Password"] ?? "cashflow";

        cfg.Host(host, vhost, h =>
        {
            h.Username(user);
            h.Password(pwd);
        });

        cfg.UseMessageRetry(r => r.Exponential(
            retryLimit: 5,
            minInterval: TimeSpan.FromSeconds(1),
            maxInterval: TimeSpan.FromSeconds(30),
            intervalDelta: TimeSpan.FromSeconds(2)));

        // Producer-only. ConfigureEndpoints intentionally NOT called — Ledger não consome.
    });
});

// Ledger é producer-only (sem InboxState — ver ADR-0007). O InboxCleanupService que o
// AddEntityFrameworkOutbox registra tenta abrir o DbSet<InboxState> a cada ciclo e gera
// retries barulhentos. Como não temos consumer, desligamos a sweep aqui.
var inboxCleanupDescriptor = builder.Services.FirstOrDefault(d =>
    d.ImplementationType is { Name: "InboxCleanupService`1" } t
    && t.Namespace == "MassTransit.EntityFrameworkCoreIntegration");
if (inboxCleanupDescriptor is not null)
    builder.Services.Remove(inboxCleanupDescriptor);

// ====== Authentication / Authorization ======
var keycloakAuthority = builder.Configuration["Keycloak:Authority"]
    ?? throw new InvalidOperationException("Keycloak:Authority missing");
var keycloakAudience = builder.Configuration["Keycloak:Audience"] ?? "cashflow-api";
var requireHttpsMetadata = builder.Configuration.GetValue<bool?>("Keycloak:RequireHttpsMetadata") ?? !builder.Environment.IsDevelopment();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakAuthority;
        options.Audience = keycloakAudience;
        options.RequireHttpsMetadata = requireHttpsMetadata;
        // Sem o mapping, o claim "role" do Keycloak vira ClaimTypes.Role
        // e quebra IsInRole/RoleClaimType="role" — ver Authorization/AuthorizationPolicies.
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

builder.Services.AddCashflowAuthorization();

// ====== HealthChecks (Postgres + RabbitMQ + Keycloak JWKS) ======
var postgresConn = builder.Configuration.GetConnectionString("Postgres")!;
var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitPort = int.TryParse(builder.Configuration["RabbitMq:Port"], out var rp) ? rp : 5672;
var keycloakJwksUrl = $"{keycloakAuthority.TrimEnd('/')}/protocol/openid-connect/certs";

builder.Services.AddHealthChecks()
    .AddNpgSql(postgresConn, name: "postgres", tags: new[] { "ready", "db" })
    .AddCheck("rabbitmq", new RabbitMqHealthCheck(rabbitHost, rabbitPort), tags: new[] { "ready", "broker" })
    .AddUrlGroup(new Uri(keycloakJwksUrl), name: "keycloak-jwks", tags: new[] { "ready", "auth" });

// ====== Rate limiter (defense-in-depth — Gateway é o teto principal) ======
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opt.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
    {
        var key = http.GetMerchantId()?.ToString()
            ?? http.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromSeconds(1),
            SegmentsPerWindow = 4,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });
});

// ====== Problem Details (RFC 7807) ======
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
builder.Services.AddTransient<IdempotencyKeyEndpointFilter>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Cashflow Ledger API",
        Version = "v1",
        Description = "Write-side API para o módulo de Lançamentos."
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

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapEntriesEndpoints();
app.MapAdminEndpoints();

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

Console.WriteLine("[BOOT] app built, environment=" + app.Environment.EnvironmentName);
var diagConn = app.Configuration.GetConnectionString("Postgres") ?? "(null)";
var sanitized = System.Text.RegularExpressions.Regex.Replace(diagConn, "(?i)Password=[^;]*", "Password=***");
Console.WriteLine("[BOOT] Postgres connection string in use: " + sanitized);
Console.Out.Flush();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapScalarApiReference(o => o.Title = "Cashflow Ledger");
}

// Inicia o host (Kestrel + MassTransit). Depois aplica migrations em Dev.
Console.WriteLine("[BOOT] starting host...");
Console.Out.Flush();
await app.StartAsync().ConfigureAwait(false);
Console.WriteLine("[BOOT] host started; addresses=" + string.Join(",", app.Urls));
Console.Out.Flush();

if (app.Environment.IsDevelopment())
{
    // Apply migrations on startup — Development only (§05 §1.6).
    // Constrói o DbContext manualmente para evitar deadlock que ocorre ao resolver
    // IPublishEndpoint pelo container DI enquanto o BusOutbox EF ainda está nas
    // últimas etapas da inicialização do bus.
    Console.WriteLine("[BOOT] applying migrations...");
    Console.Out.Flush();
    try
    {
        var connectionString = app.Configuration.GetConnectionString("Postgres")!;
        var opts = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(connectionString, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "ledger"))
            .Options;
        // Empty IServiceProvider: migrations não disparam SaveChangesAsync, então
        // o IPublishEndpoint nunca é resolvido. Evita o ciclo com BusOutbox.
        // ASP0000 é intencional aqui: precisamos de um SP isolado para migrar sem
        // tocar o container principal.
#pragma warning disable ASP0000
        await using var db = new LedgerDbContext(opts, new ServiceCollection().BuildServiceProvider(), app.Services.GetRequiredService<IClock>());
#pragma warning restore ASP0000
        Console.WriteLine("[BOOT][mig] DbContext created manually");
        Console.Out.Flush();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await db.Database.MigrateAsync(cts.Token).ConfigureAwait(false);
        Console.WriteLine("[BOOT] migrations applied");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("[BOOT][ERROR] migration failed: " + ex);
        Console.Error.Flush();
        await app.StopAsync().ConfigureAwait(false);
        throw;
    }
    Console.Out.Flush();
}

Console.WriteLine("[BOOT] waiting for shutdown");
Console.Out.Flush();
await app.WaitForShutdownAsync().ConfigureAwait(false);

// Exposed para WebApplicationFactory em integration tests futuros.
public partial class Program;
