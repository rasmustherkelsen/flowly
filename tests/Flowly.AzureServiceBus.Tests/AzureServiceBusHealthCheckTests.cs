using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flowly.AzureServiceBus.Tests;

public class AzureServiceBusHealthCheckTests
{
    public class CheckHealthAsync
    {
        [Fact]
        public async Task WhenSomethingIsListeningOnThePort_ReturnsHealthy()
        {
            using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            var acceptTask = tcpListener.AcceptTcpClientAsync();

            var azureServiceBusHealthCheck = new AzureServiceBusHealthCheck(IPAddress.Loopback.ToString(), port);

            var result = await azureServiceBusHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);

            using var acceptedClient = await acceptTask;
        }

        [Fact]
        public async Task WhenNothingIsListeningOnThePort_ReturnsUnhealthy()
        {
            var port = GetUnusedPort();

            var azureServiceBusHealthCheck = new AzureServiceBusHealthCheck(IPAddress.Loopback.ToString(), port);

            var result = await azureServiceBusHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.NotNull(result.Exception);
        }

        [Fact]
        public async Task WhenHostCannotBeResolved_ReturnsUnhealthy()
        {
            var azureServiceBusHealthCheck = new AzureServiceBusHealthCheck("this-host-does-not-resolve.invalid", 5671);

            var result = await azureServiceBusHealthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.NotNull(result.Exception);
        }

        private static int GetUnusedPort()
        {
            using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();

            return ((IPEndPoint)tcpListener.LocalEndpoint).Port;
        }
    }
}
