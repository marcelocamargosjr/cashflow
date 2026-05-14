using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cashflow.Consolidation.Api.Infrastructure;

internal sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly string _host;
    private readonly int _port;

    public RabbitMqHealthCheck(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(_host, _port, timeout.Token).ConfigureAwait(false);
            return HealthCheckResult.Healthy($"AMQP socket reachable at {_host}:{_port}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"AMQP socket unreachable at {_host}:{_port}", ex);
        }
    }
}
