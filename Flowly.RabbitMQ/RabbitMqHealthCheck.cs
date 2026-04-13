using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flowly.RabbitMQ;

internal sealed class RabbitMqHealthCheck(RabbitMqLazyConnection lazyConnection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await lazyConnection.GetAsync(cancellationToken);

            return connection.IsOpen
                ? HealthCheckResult.Healthy("RabbitMQ connection is open.")
                : HealthCheckResult.Unhealthy("RabbitMQ connection is closed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ connection failed.", ex);
        }
    }
}
