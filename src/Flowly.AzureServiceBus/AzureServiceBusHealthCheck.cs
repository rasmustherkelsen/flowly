using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flowly.AzureServiceBus;

/// <summary>
/// Health check that verifies a TCP connection can be opened to the configured Azure Service Bus <paramref name="host"/>/<paramref name="port"/>.
/// </summary>
/// <param name="host">The Service Bus namespace host (or emulator host) to connect to.</param>
/// <param name="port">The port to connect to.</param>
/// <remarks>
/// <para>
/// This check proves only that <em>some</em> process accepts a TCP connection on <paramref name="host"/>/<paramref name="port"/> —
/// it opens a <see cref="TcpClient"/>, connects, and immediately closes the socket. It performs no AMQP protocol
/// handshake, no TLS negotiation, and no authentication against the namespace. Consequently it can report
/// <see cref="HealthCheckResult.Healthy"/> even when the namespace is unusable at the application layer: a
/// misconfigured/wrong namespace whose DNS name still resolves to a host with an open port, a namespace the
/// caller cannot authenticate against, or an emulator whose control plane has crashed while its network listener
/// is still bound would all still pass this check. It can equally report <see cref="HealthCheckResult.Unhealthy"/>
/// due to a transient network blip unrelated to the namespace's actual availability.
/// </para>
/// <para>
/// A deeper, AMQP-level or authenticated check (e.g. constructing a short-lived <c>ServiceBusClient</c> and
/// calling <c>GetQueueRuntimePropertiesAsync</c> for a known entity) is intentionally not implemented here: this
/// type receives only <paramref name="host"/>/<paramref name="port"/> — no connection string, credential, or
/// admin client — so it has nothing to authenticate with. A raw-socket AMQP protocol header exchange was also
/// considered and rejected: the port this check is given for real (non-emulator) namespaces is chosen for
/// firewall-friendly reachability and is not guaranteed to be the port the SDK-managed <c>ServiceBusClient</c>
/// actually negotiates AMQP traffic on, and on such a TLS-terminated port a plaintext AMQP header is not a valid
/// TLS record — the endpoint would simply close the connection, turning a healthy namespace into a false
/// <see cref="HealthCheckResult.Unhealthy"/> result. Doing this correctly would require implementing a TLS
/// handshake and, for namespaces reachable only over AMQP-over-WebSockets, an HTTP upgrade and WebSocket framing
/// by hand — reimplementing transport logic that only the Service Bus SDK should own, which is out of proportion
/// for a health check. Treat a <see cref="HealthCheckResult.Healthy"/> result from this check as "the network
/// path to the configured host/port is open", not as "the Service Bus namespace is fully operational".
/// </para>
/// </remarks>
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
