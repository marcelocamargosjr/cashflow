using Cashflow.Ledger.Api.Authorization;
using Cashflow.Ledger.Api.Endpoints;
using Cashflow.Ledger.Api.Infrastructure;
using Cashflow.Ledger.Application;
using Cashflow.Ledger.Infrastructure;
using Cashflow.Ledger.Infrastructure.Persistence;
using Cashflow.SharedKernel.Http;
using Cashflow.SharedKernel.Observability;
using Cashflow.SharedKernel.Time;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddCashflowObservability("cashflow.ledger.api", "1.0.0");

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddLedgerApplication();
builder.Services.AddLedgerInfrastructure(builder.Configuration);
builder.Services.AddLedgerMessaging(builder.Configuration);
builder.Services.AddLedgerAuth(builder.Configuration, builder.Environment);
builder.Services.AddLedgerHealthChecks(builder.Configuration);
builder.Services.AddLedgerRateLimiter();
builder.Services.AddLedgerProblemDetails();
builder.Services.AddLedgerSwagger();

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

var bootLogger = app.Logger;
bootLogger.LogInformation("App built. Environment={Environment}", app.Environment.EnvironmentName);
var diagConn = app.Configuration.GetConnectionString("Postgres") ?? "(null)";
// S2068 / MA0009 silenciados: o literal "Password=" é um pattern de mascaramento
// para o log de conn-string, não uma credencial hardcoded. RegexOptions.NonBacktracking
// + timeout de 1s blindam contra ReDoS no diagnóstico de boot.
#pragma warning disable S2068
var sanitized = System.Text.RegularExpressions.Regex.Replace(
    diagConn,
    "(?i)Password=[^;]*",
    "Password=***",
    System.Text.RegularExpressions.RegexOptions.NonBacktracking,
    TimeSpan.FromSeconds(1));
#pragma warning restore S2068
bootLogger.LogInformation("Postgres connection string in use: {ConnectionString}", sanitized);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapScalarApiReference(o => o.Title = "Cashflow Ledger");

    // Apply migrations on startup — Development only (§05 §1.6).
    // Constrói o DbContext manualmente para evitar deadlock que ocorre ao resolver
    // IPublishEndpoint pelo container DI enquanto o BusOutbox EF ainda está nas
    // últimas etapas da inicialização do bus.
    bootLogger.LogInformation("Applying migrations...");
    // S2139 silenciado: catch loga LogCritical e re-emite para falhar o boot.
    // Não há "handle" possível aqui — uma migration que falha exige human review.
#pragma warning disable S2139
    try
    {
        var connectionString = app.Configuration.GetConnectionString("Postgres")!;
        var opts = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(connectionString, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "ledger"))
            .Options;
        // Empty IServiceProvider: migrations não disparam SaveChangesAsync, então
        // o IPublishEndpoint nunca é resolvido. Evita o ciclo com BusOutbox.
#pragma warning disable ASP0000
        var db = new LedgerDbContext(opts, new ServiceCollection().BuildServiceProvider(), app.Services.GetRequiredService<IClock>());
#pragma warning restore ASP0000
        await using (db.ConfigureAwait(false))
        {
            bootLogger.LogDebug("DbContext created manually");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await db.Database.MigrateAsync(cts.Token).ConfigureAwait(false);
            bootLogger.LogInformation("Migrations applied");
        }
    }
    catch (Exception ex)
    {
        bootLogger.LogCritical(ex, "Migration failed during startup");
        throw;
    }
#pragma warning restore S2139
}

bootLogger.LogInformation("Starting host...");
await app.RunAsync().ConfigureAwait(false);

// Exposed para WebApplicationFactory em integration tests. Namespace explícito
// evita ambiguidade quando outros services também declaram `partial class Program`.
namespace Cashflow.Ledger.Api
{
    public partial class Program;
}
