using Cashflow.Consolidation.Api.Authorization;
using Cashflow.Consolidation.Api.Endpoints;
using Cashflow.Consolidation.Api.Infrastructure;
using Cashflow.Consolidation.Application;
using Cashflow.Consolidation.Infrastructure;
using Cashflow.Consolidation.Infrastructure.Caching;
using Cashflow.SharedKernel.Http;
using Cashflow.SharedKernel.Observability;
using Cashflow.SharedKernel.Time;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddCashflowObservability("cashflow.consolidation.api", "1.0.0");

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddConsolidationApplication();
builder.Services.AddConsolidationInfrastructure(builder.Configuration);
builder.Services.AddConsolidationCache(builder.Configuration);
builder.Services.AddConsolidationAuth(builder.Configuration, builder.Environment);
builder.Services.AddConsolidationHealthChecks(builder.Configuration);
builder.Services.AddConsolidationProblemDetails();
builder.Services.AddConsolidationSwagger();

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
