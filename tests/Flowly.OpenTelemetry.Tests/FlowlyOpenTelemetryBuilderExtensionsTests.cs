using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Flowly.OpenTelemetry.Tests;

public class FlowlyOpenTelemetryBuilderExtensionsTests
{
    public class AddFlowlyOpenTelemetry
    {
        [Fact]
        public void WithoutServiceName_LeavesSdkDefaultServiceName()
        {
            var builder = HostBuilder();

            builder.AddFlowlyOpenTelemetry();

            Assert.StartsWith("unknown_service", ServiceName(builder));
        }

        [Fact]
        public void WithServiceName_SetsServiceName()
        {
            var builder = HostBuilder();

            builder.AddFlowlyOpenTelemetry("MyApp.Receiver");

            Assert.Equal("MyApp.Receiver", ServiceName(builder));
        }

        [Fact]
        public void WithServiceNameAndOtelServiceNameConfigured_UsesOtelServiceName()
        {
            var builder = HostBuilder(("OTEL_SERVICE_NAME", "orders-receiver"));

            builder.AddFlowlyOpenTelemetry("MyApp.Receiver");

            Assert.Equal("orders-receiver", ServiceName(builder));
        }

        [Fact]
        public void WithServiceNameAndBlankOtelServiceName_UsesServiceName()
        {
            var builder = HostBuilder(("OTEL_SERVICE_NAME", ""));

            builder.AddFlowlyOpenTelemetry("MyApp.Receiver");

            Assert.Equal("MyApp.Receiver", ServiceName(builder));
        }

        [Fact]
        public void WithServiceNameAndServiceNameInResourceAttributes_UsesResourceAttribute()
        {
            var builder = HostBuilder(("OTEL_RESOURCE_ATTRIBUTES", "service.name=orders-receiver"));

            builder.AddFlowlyOpenTelemetry("MyApp.Receiver");

            Assert.Equal("orders-receiver", ServiceName(builder));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void WithBlankServiceName_Throws(string serviceName) =>
            Assert.Throws<ArgumentException>(() => HostBuilder().AddFlowlyOpenTelemetry(serviceName));
    }

    private static HostApplicationBuilder HostBuilder(params (string Key, string Value)[] values)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value)));

        return builder;
    }

    private static string? ServiceName(HostApplicationBuilder builder)
    {
        using var host = builder.Build();

        return host.Services.GetRequiredService<TracerProvider>().GetResource().Attributes
            .SingleOrDefault(attribute => attribute.Key == "service.name").Value as string;
    }
}
