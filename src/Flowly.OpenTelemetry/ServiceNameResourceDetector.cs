using Microsoft.Extensions.Configuration;
using OpenTelemetry.Resources;

namespace Flowly.OpenTelemetry;

/// <summary>
///     Supplies a default <c>service.name</c> resource attribute that yields to the standard OpenTelemetry
///     environment configuration: when <c>OTEL_SERVICE_NAME</c> or a <c>service.name</c> entry in
///     <c>OTEL_RESOURCE_ATTRIBUTES</c> is set, the detector contributes nothing and the SDK's own detectors
///     decide the name.
/// </summary>
internal sealed class ServiceNameResourceDetector(string serviceName, IConfiguration? configuration) : IResourceDetector
{
    private const string ServiceNameAttribute = "service.name";
    private const string ServiceNameVariable = "OTEL_SERVICE_NAME";
    private const string ResourceAttributesVariable = "OTEL_RESOURCE_ATTRIBUTES";

    public Resource Detect()
    {
        if (!string.IsNullOrWhiteSpace(Read(ServiceNameVariable)) || ResourceAttributesContainServiceName(Read(ResourceAttributesVariable)))
        {
            return Resource.Empty;
        }

        return new Resource([new KeyValuePair<string, object>(ServiceNameAttribute, serviceName)]);
    }

    private string? Read(string key) =>
        configuration is null ? Environment.GetEnvironmentVariable(key) : configuration[key];

    private static bool ResourceAttributesContainServiceName(string? resourceAttributes)
    {
        if (string.IsNullOrWhiteSpace(resourceAttributes))
        {
            return false;
        }

        foreach (var pair in resourceAttributes.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator > 0
                && pair[..separator].Trim() == ServiceNameAttribute
                && !string.IsNullOrWhiteSpace(pair[(separator + 1)..]))
            {
                return true;
            }
        }

        return false;
    }
}
