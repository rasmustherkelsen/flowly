using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flowly.RabbitMQ;

internal sealed class RabbitMqHealthCheck(IRabbitMqConnectionPool connectionPool) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var publisherConnection = await connectionPool.GetPublisherConnection(cancellationToken);
            var consumerConnection = await connectionPool.GetConsumerConnection(cancellationToken);

            if (!publisherConnection.IsOpen)
                return HealthCheckResult.Unhealthy("RabbitMQ publisher connection is closed.");

            if (!consumerConnection.IsOpen)
                return HealthCheckResult.Unhealthy("RabbitMQ consumer connection is closed.");

            return HealthCheckResult.Healthy("RabbitMQ connections are open.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ connection failed.", ex);
        }
    }
}
