using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flowly.AzureServiceBus;

internal sealed class AzureServiceBusHealthCheck(string host, int port) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, port, cancellationToken);

            return HealthCheckResult.Healthy("Azure Service Bus endpoint is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Azure Service Bus endpoint is not reachable.", ex);
        }
    }
}
