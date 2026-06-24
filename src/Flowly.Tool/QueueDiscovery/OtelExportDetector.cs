using System.Text.Json;

namespace Flowly.Tool.QueueDiscovery;

internal static class OtelExportDetector
{
    private const string ZipkinDll = "OpenTelemetry.Exporter.Zipkin.dll";
    private const string OtlpDll = "OpenTelemetry.Exporter.OpenTelemetryProtocol.dll";

    public static IReadOnlySet<string> Detect(IReadOnlyList<QueueDiscoverySource> sources)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            var outputDir = Path.GetDirectoryName(source.Assembly.FullName)!;

            if (File.Exists(Path.Combine(outputDir, ZipkinDll)))
            {
                result.Add("Zipkin");
            }

            if (File.Exists(Path.Combine(outputDir, OtlpDll)) && HasLocalhostOtlpEndpoint(outputDir))
            {
                result.Add("Jaeger");
            }
        }

        return result;
    }

    private static bool HasLocalhostOtlpEndpoint(string outputDir)
    {
        var projectRoot = FindProjectRoot(outputDir);

        if (projectRoot is null)
        {
            return false;
        }

        var launchSettings = Path.Combine(projectRoot, "Properties", "launchSettings.json");

        if (!File.Exists(launchSettings))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(launchSettings);
            using var doc = JsonDocument.Parse(stream);

            if (!doc.RootElement.TryGetProperty("profiles", out var profiles))
            {
                return false;
            }

            foreach (var profile in profiles.EnumerateObject())
            {
                if (!profile.Value.TryGetProperty("environmentVariables", out var envVars))
                {
                    continue;
                }

                if (!envVars.TryGetProperty("OTEL_EXPORTER_OTLP_ENDPOINT", out var endpoint))
                {
                    continue;
                }

                var value = endpoint.GetString();

                if (value is not null && IsLocalhostEndpoint(value))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool IsLocalhostEndpoint(string endpoint) =>
        endpoint.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
        endpoint.Contains("127.0.0.1");

    private static string? FindProjectRoot(string outputDir)
    {
        var dir = new DirectoryInfo(outputDir);

        while (dir is not null)
        {
            if (dir.GetFiles("*.csproj").Length > 0)
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
