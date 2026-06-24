using System.CommandLine;
using Flowly.Tool.Generation;
using Flowly.Tool.ProjectResolution;
using Flowly.Tool.QueueDiscovery;

namespace Flowly.Tool.Commands;

internal static class DockerComposeCommand
{
    public static Command Build(SharedOptions shared)
    {
        var namespaceOption = new Option<string>("--namespace")
        {
            Description = "Service Bus namespace name in emulator config (default: sbemulatorns)"
        };

        var sbconfigOutputOption = new Option<FileInfo?>("--sbconfig-output")
        {
            Description = "Output path for sbconfig.json when Azure Service Bus is detected. Defaults to sbconfig.json next to --output."
        };

        var otelExportOption = new Option<string?>("--otel-export")
        {
            Description = "Override auto-detected OTel export target (jaeger or zipkin). Auto-detection checks for localhost OTEL_EXPORTER_OTLP_ENDPOINT in launchSettings.json (Jaeger) and OpenTelemetry.Exporter.Zipkin.dll (Zipkin)."
        };

        var command = new Command("docker-compose", "Generate a docker-compose.yml with local development dependencies based on the transports and database providers used by the project(s)");
        command.Add(shared.Assembly);
        command.Add(shared.Project);
        command.Add(shared.Configuration);
        command.Add(shared.Framework);
        command.Add(shared.NoBuild);
        command.Add(shared.ConfigurationType);
        command.Add(shared.WorkingDirectory);
        command.Add(shared.Output);
        command.Add(namespaceOption);
        command.Add(sbconfigOutputOption);
        command.Add(otelExportOption);
        command.SetAction(parseResult => CommandExecutor.ExecuteSafely(() =>
        {
            var sources = ProjectAssemblyResolver.Resolve(parseResult, shared);
            var configurationType = parseResult.GetValue(shared.ConfigurationType);
            var workingDirectory = parseResult.GetValue(shared.WorkingDirectory);
            var output = parseResult.GetValue(shared.Output);
            var @namespace = parseResult.GetValue(namespaceOption) ?? "sbemulatorns";
            var sbconfigOutput = parseResult.GetValue(sbconfigOutputOption);
            var otelExportOverride = parseResult.GetValue(otelExportOption);

            var transportTypes = TransportDetector.Detect(sources);
            var databaseProviders = DatabaseProviderDetector.Detect(sources);
            var otelExportTargets = ResolveOtelExportTargets(sources, otelExportOverride);

            var hasAsb = transportTypes.Contains("AzureServiceBus", StringComparer.OrdinalIgnoreCase);

            string? sbconfigJson = null;
            FileInfo? resolvedSbconfigOutput = null;

            if (hasAsb)
            {
                var (queueDefinitions, eventDefinitions, _) = QueueDiscoveryRunner.DiscoverQueues(
                    sources,
                    configurationType,
                    workingDirectory,
                    "azure-service-bus");

                sbconfigJson = AzureServiceBusOutputGenerator.CreateEmulatorConfigJson(@namespace, queueDefinitions, eventDefinitions);

                resolvedSbconfigOutput = sbconfigOutput
                    ?? (output is not null
                        ? new FileInfo(Path.Combine(output.DirectoryName ?? ".", "sbconfig.json"))
                        : null);
            }

            var sbconfigRelativePath = resolvedSbconfigOutput is not null
                ? $"./{resolvedSbconfigOutput.Name}"
                : "./sbconfig.json";

            var dockerCompose = DockerComposeGenerator.Generate(transportTypes, databaseProviders, sbconfigRelativePath, otelExportTargets);
            OutputWriter.Write(dockerCompose, output);

            if (sbconfigJson is not null)
            {
                if (resolvedSbconfigOutput is not null)
                {
                    OutputWriter.Write(sbconfigJson, resolvedSbconfigOutput);
                }
                else
                {
                    Console.Error.WriteLine("Azure Service Bus detected. Run 'flowly azure-service-bus emulator-config' to generate sbconfig.json.");
                }
            }
        }));

        return command;
    }

    private static IReadOnlySet<string> ResolveOtelExportTargets(
        IReadOnlyList<QueueDiscoverySource> sources,
        string? otelExportOverride)
    {
        if (otelExportOverride is not null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { otelExportOverride };
        }

        return OtelExportDetector.Detect(sources);
    }
}
