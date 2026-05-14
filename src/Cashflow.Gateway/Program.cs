using Cashflow.Gateway;
using Cashflow.SharedKernel.Http;
using Cashflow.SharedKernel.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.AddCashflowObservability("cashflow.gateway", "1.0.0");

builder.Services.AddGatewayAuth(builder.Configuration, builder.Environment);
builder.Services.AddGatewayRateLimiter();
builder.Services.AddGatewayHealthChecks(builder.Configuration);
builder.Services.AddGatewayProblemDetails();
builder.Services.AddGatewayReverseProxy(builder.Configuration);

var app = builder.Build();

app.UseCashflowCorrelationId();
app.UseStatusCodePages();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
// /health = liveness simples (compat com healthcheck do compose).
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });

app.MapReverseProxy();

await app.RunAsync().ConfigureAwait(false);

// S1118 silenciado: top-level statements em C# 9+ sintetizam um `internal partial
// class Program` no global namespace. A redeclaração public partial existe para
// que WebApplicationFactory<Program> em integration tests consiga referenciar o
// tipo. Não é uma utility class — não cabe `static` nem ctor protected.
#pragma warning disable S1118
public partial class Program;
#pragma warning restore S1118
