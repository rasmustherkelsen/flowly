using Microsoft.Extensions.Configuration;

namespace Flowly.OpenTelemetry.Tests;

public class ServiceNameResourceDetectorTests
{
    public class Detect
    {
        [Fact]
        public void WithNoServiceNameConfigured_ReturnsServiceName()
        {
            var resource = new ServiceNameResourceDetector("MyApp.Receiver", Configuration()).Detect();

            Assert.Equal("MyApp.Receiver", ServiceName(resource));
        }

        [Fact]
        public void WithOtelServiceNameSet_ReturnsEmptyResource()
        {
            var resource = new ServiceNameResourceDetector("MyApp.Receiver", Configuration(("OTEL_SERVICE_NAME", "orders-receiver"))).Detect();

            Assert.Empty(resource.Attributes);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void WithBlankOtelServiceName_ReturnsServiceName(string otelServiceName)
        {
            var resource = new ServiceNameResourceDetector("MyApp.Receiver", Configuration(("OTEL_SERVICE_NAME", otelServiceName))).Detect();

            Assert.Equal("MyApp.Receiver", ServiceName(resource));
        }

        [Theory]
        [InlineData("service.name=orders-receiver")]
        [InlineData("deployment.environment=prod, service.name = orders-receiver")]
        public void WithServiceNameInResourceAttributes_ReturnsEmptyResource(string resourceAttributes)
        {
            var resource = new ServiceNameResourceDetector("MyApp.Receiver", Configuration(("OTEL_RESOURCE_ATTRIBUTES", resourceAttributes))).Detect();

            Assert.Empty(resource.Attributes);
        }

        [Theory]
        [InlineData("deployment.environment=prod")]
        [InlineData("service.name=")]
        [InlineData("service.namespace=orders")]
        public void WithResourceAttributesWithoutServiceName_ReturnsServiceName(string resourceAttributes)
        {
            var resource = new ServiceNameResourceDetector("MyApp.Receiver", Configuration(("OTEL_RESOURCE_ATTRIBUTES", resourceAttributes))).Detect();

            Assert.Equal("MyApp.Receiver", ServiceName(resource));
        }
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();

    private static object? ServiceName(global::OpenTelemetry.Resources.Resource resource) =>
        resource.Attributes.SingleOrDefault(attribute => attribute.Key == "service.name").Value;
}
