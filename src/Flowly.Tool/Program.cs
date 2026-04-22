using Flowly.Tool.Commands;
using System.CommandLine;

var shared = new SharedOptions();

var rootCommand = new RootCommand("Flowly queue discovery and infrastructure generation");
rootCommand.Add(AzureServiceBusCommand.Build(shared));
rootCommand.Add(DockerComposeCommand.Build(shared));
rootCommand.Add(CompletionCommands.BuildCompletion());
rootCommand.Add(CompletionCommands.BuildInstallCompletion());
rootCommand.Add(CompletionCommands.BuildRemoveCompletion());

return await rootCommand.Parse(args).InvokeAsync();
