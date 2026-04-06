using System.CommandLine;
using Flowly.Tool.Generation;
using Flowly.Tool.ProjectResolution;
using Flowly.Tool.QueueDiscovery;

namespace Flowly.Tool.Commands;

internal static class AzureServiceBusCommand
{
    public static Command Build(SharedOptions shared)
    {
        var command = new Command("azure-service-bus", "Azure Service Bus specific commands");
        command.Add(BuildQueuesCommand(shared));
        command.Add(BuildEmulatorConfigCommand(shared));
        command.Add(BuildBicepCommand(shared));
        command.Add(BuildAspireCodeCommand(shared));
        return command;
    }

    private static Command BuildQueuesCommand(SharedOptions shared)
    {
        var command = new Command("queues", "Discover and print all Flowly queue names from one or more assemblies/projects");
        command.Add(shared.Assembly);
        command.Add(shared.Project);
        command.Add(shared.Configuration);
        command.Add(shared.Framework);
        command.Add(shared.NoBuild);
        command.Add(shared.ConfigurationType);
        command.Add(shared.WorkingDirectory);
        command.SetAction(parseResult => CommandExecutor.ExecuteSafely(() =>
        {
            var sources = ProjectAssemblyResolver.Resolve(parseResult, shared);
            var configurationType = parseResult.GetValue(shared.ConfigurationType);
            var workingDirectory = parseResult.GetValue(shared.WorkingDirectory);

            var (queueDefinitions, configurationTypes) = QueueDiscoveryRunner.DiscoverQueues(sources, configurationType, workingDirectory);

            if (configurationTypes.Count == 1)
            {
                Console.WriteLine($"Configuration: {configurationTypes[0]}");
            }
            else
            {
                Console.WriteLine("Configurations:");
                foreach (var configuration in configurationTypes)
                {
                    Console.WriteLine($"- {configuration}");
                }
            }

            foreach (var queue in queueDefinitions.Select(x => x.Name))
            {
                Console.WriteLine(queue);
            }
        }));

        return command;
    }

    private static Command BuildEmulatorConfigCommand(SharedOptions shared)
    {
        var namespaceOption = new Option<string>("--namespace");
        namespaceOption.Description = "Service Bus namespace name in emulator config";

        var command = new Command("emulator-config", "Generate Azure Service Bus emulator config JSON");
        command.Add(shared.Assembly);
        command.Add(shared.Project);
        command.Add(shared.Configuration);
        command.Add(shared.Framework);
        command.Add(shared.NoBuild);
        command.Add(shared.ConfigurationType);
        command.Add(shared.WorkingDirectory);
        command.Add(shared.Output);
        command.Add(namespaceOption);
        command.SetAction(parseResult => CommandExecutor.ExecuteSafely(() =>
        {
            var sources = ProjectAssemblyResolver.Resolve(parseResult, shared);
            var configurationType = parseResult.GetValue(shared.ConfigurationType);
            var workingDirectory = parseResult.GetValue(shared.WorkingDirectory);
            var output = parseResult.GetValue(shared.Output);
            var @namespace = parseResult.GetValue(namespaceOption) ?? "sbemulatorns";

            var (queueDefinitions, _) = QueueDiscoveryRunner.DiscoverQueues(sources, configurationType, workingDirectory);
            var json = AzureServiceBusOutputGenerator.CreateEmulatorConfigJson(@namespace, queueDefinitions);
            OutputWriter.Write(json, output);
        }));

        return command;
    }

    private static Command BuildBicepCommand(SharedOptions shared)
    {
        var namespaceResourceNameOption = new Option<string>("--namespace-resource-name");
        namespaceResourceNameOption.Description = "Bicep resource symbol name for the Service Bus namespace";

        var namespaceNameOption = new Option<string>("--service-bus-namespace-name");
        namespaceNameOption.Description = "Azure Service Bus namespace name value";

        var command = new Command("bicep", "Generate Bicep template for Azure Service Bus queues");
        command.Add(shared.Assembly);
        command.Add(shared.Project);
        command.Add(shared.Configuration);
        command.Add(shared.Framework);
        command.Add(shared.NoBuild);
        command.Add(shared.ConfigurationType);
        command.Add(shared.WorkingDirectory);
        command.Add(shared.Output);
        command.Add(namespaceResourceNameOption);
        command.Add(namespaceNameOption);
        command.SetAction(parseResult => CommandExecutor.ExecuteSafely(() =>
        {
            var sources = ProjectAssemblyResolver.Resolve(parseResult, shared);
            var configurationType = parseResult.GetValue(shared.ConfigurationType);
            var workingDirectory = parseResult.GetValue(shared.WorkingDirectory);
            var output = parseResult.GetValue(shared.Output);
            var namespaceResourceName = parseResult.GetValue(namespaceResourceNameOption) ?? "serviceBusNamespace";
            var serviceBusNamespaceName = parseResult.GetValue(namespaceNameOption) ?? "sb-flowly";

            var (queueDefinitions, _) = QueueDiscoveryRunner.DiscoverQueues(sources, configurationType, workingDirectory);
            var bicep = AzureServiceBusOutputGenerator.CreateBicepTemplate(namespaceResourceName, serviceBusNamespaceName, queueDefinitions);
            OutputWriter.Write(bicep, output);
        }));

        return command;
    }

    private static Command BuildAspireCodeCommand(SharedOptions shared)
    {
        var builderVariableOption = new Option<string>("--builder-variable");
        builderVariableOption.Description = "Variable name of DistributedApplicationBuilder in generated code";

        var connectionNameOption = new Option<string>("--connection-name");
        connectionNameOption.Description = "Connection/resource name passed to AddAzureServiceBus";

        var namespaceVariableOption = new Option<string>("--namespace-variable");
        namespaceVariableOption.Description = "Variable name assigned to AddAzureServiceBus result";

        var command = new Command("aspire-code", "Generate C# bootstrap code for Azure Service Bus queue registration in Aspire");
        command.Add(shared.Assembly);
        command.Add(shared.Project);
        command.Add(shared.Configuration);
        command.Add(shared.Framework);
        command.Add(shared.NoBuild);
        command.Add(shared.ConfigurationType);
        command.Add(shared.WorkingDirectory);
        command.Add(shared.Output);
        command.Add(builderVariableOption);
        command.Add(connectionNameOption);
        command.Add(namespaceVariableOption);
        command.SetAction(parseResult => CommandExecutor.ExecuteSafely(() =>
        {
            var sources = ProjectAssemblyResolver.Resolve(parseResult, shared);
            var configurationType = parseResult.GetValue(shared.ConfigurationType);
            var workingDirectory = parseResult.GetValue(shared.WorkingDirectory);
            var output = parseResult.GetValue(shared.Output);
            var builderVariable = parseResult.GetValue(builderVariableOption) ?? "builder";
            var connectionName = parseResult.GetValue(connectionNameOption) ?? "EmulatorNamespace";
            var namespaceVariable = parseResult.GetValue(namespaceVariableOption) ?? "azureServiceBus";

            var (queueDefinitions, _) = QueueDiscoveryRunner.DiscoverQueues(sources, configurationType, workingDirectory);
            var code = AzureServiceBusOutputGenerator.CreateAspireBootstrapCode(
                builderVariable,
                connectionName,
                namespaceVariable,
                queueDefinitions.Select(x => x.Name).ToArray());
            OutputWriter.Write(code, output);
        }));

        return command;
    }
}
