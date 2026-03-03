using Flowly.Tool.Generation;
using Flowly.Tool.QueueDiscovery;
using System.Diagnostics;
using System.CommandLine;

var assemblyOption = new Option<string[]>("--assembly", new[] { "-a" })
{
    Required = false
};
assemblyOption.Description = "Path to a compiled assembly containing Flowly configuration (can be specified multiple times)";

var projectOption = new Option<string[]>("--project", new[] { "-p" });
projectOption.Description = "Path to a .csproj file or a folder containing a single .csproj (can be specified multiple times)";

var configurationOption = new Option<string>("--configuration");
configurationOption.Description = "Build configuration used when --project is provided";

var frameworkOption = new Option<string?>("--framework");
frameworkOption.Description = "Target framework used when --project is provided (for multi-target projects)";

var noBuildOption = new Option<bool>("--no-build");
noBuildOption.Description = "Do not build the project before resolving target assembly";

var configurationTypeOption = new Option<string?>("--configuration-type", new[] { "-t" });
configurationTypeOption.Description = "Full type name or short type name of the FlowlyDesignTimeFactory-derived configuration class";

var workingDirectoryOption = new Option<DirectoryInfo?>("--working-directory", new[] { "-w" });
workingDirectoryOption.Description = "Working directory used when running configuration (defaults to assembly directory)";

var outputOption = new Option<FileInfo?>("--output", new[] { "-o" });
outputOption.Description = "Optional output file. If omitted, content is written to stdout";

var rootCommand = new RootCommand("Flowly queue discovery and infrastructure generation");
var azureServiceBusCommand = new Command("azure-service-bus", "Azure Service Bus specific commands");

var queuesCommand = new Command("queues", "Discover and print all Flowly queue names from one or more assemblies/projects");
queuesCommand.Add(assemblyOption);
queuesCommand.Add(projectOption);
queuesCommand.Add(configurationOption);
queuesCommand.Add(frameworkOption);
queuesCommand.Add(noBuildOption);
queuesCommand.Add(configurationTypeOption);
queuesCommand.Add(workingDirectoryOption);
queuesCommand.SetAction(parseResult =>
    ExecuteSafely(() =>
    {
        var assemblies = ResolveAssemblies(parseResult, assemblyOption, projectOption, configurationOption, frameworkOption, noBuildOption);
        var configurationType = parseResult.GetValue(configurationTypeOption);
        var workingDirectory = parseResult.GetValue(workingDirectoryOption);

        var (queueDefinitions, configurationTypes) = DiscoverQueuesFromAssemblies(assemblies, configurationType, workingDirectory);

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

var emulatorNamespaceOption = new Option<string>("--namespace");
emulatorNamespaceOption.Description = "Service Bus namespace name in emulator config";
var emulatorConfigCommand = new Command("emulator-config", "Generate Azure Service Bus emulator config JSON");
emulatorConfigCommand.Add(assemblyOption);
emulatorConfigCommand.Add(projectOption);
emulatorConfigCommand.Add(configurationOption);
emulatorConfigCommand.Add(frameworkOption);
emulatorConfigCommand.Add(noBuildOption);
emulatorConfigCommand.Add(configurationTypeOption);
emulatorConfigCommand.Add(workingDirectoryOption);
emulatorConfigCommand.Add(outputOption);
emulatorConfigCommand.Add(emulatorNamespaceOption);
emulatorConfigCommand.SetAction(parseResult =>
    ExecuteSafely(() =>
    {
        var assemblies = ResolveAssemblies(parseResult, assemblyOption, projectOption, configurationOption, frameworkOption, noBuildOption);
        var configurationType = parseResult.GetValue(configurationTypeOption);
        var workingDirectory = parseResult.GetValue(workingDirectoryOption);
        var output = parseResult.GetValue(outputOption);
        var @namespace = parseResult.GetValue(emulatorNamespaceOption) ?? "sbemulatorns";

        var (queueDefinitions, _) = DiscoverQueuesFromAssemblies(assemblies, configurationType, workingDirectory);
        var json = AzureServiceBusOutputGenerator.CreateEmulatorConfigJson(@namespace, queueDefinitions);
        WriteOutput(json, output);
    }));

var bicepNamespaceResourceNameOption = new Option<string>("--namespace-resource-name");
bicepNamespaceResourceNameOption.Description = "Bicep resource symbol name for the Service Bus namespace";

var bicepNamespaceNameOption = new Option<string>("--service-bus-namespace-name");
bicepNamespaceNameOption.Description = "Azure Service Bus namespace name value";
var bicepCommand = new Command("bicep", "Generate Bicep template for Azure Service Bus queues");
bicepCommand.Add(assemblyOption);
bicepCommand.Add(projectOption);
bicepCommand.Add(configurationOption);
bicepCommand.Add(frameworkOption);
bicepCommand.Add(noBuildOption);
bicepCommand.Add(configurationTypeOption);
bicepCommand.Add(workingDirectoryOption);
bicepCommand.Add(outputOption);
bicepCommand.Add(bicepNamespaceResourceNameOption);
bicepCommand.Add(bicepNamespaceNameOption);
bicepCommand.SetAction(parseResult =>
    ExecuteSafely(() =>
    {
        var assemblies = ResolveAssemblies(parseResult, assemblyOption, projectOption, configurationOption, frameworkOption, noBuildOption);
        var configurationType = parseResult.GetValue(configurationTypeOption);
        var workingDirectory = parseResult.GetValue(workingDirectoryOption);
        var output = parseResult.GetValue(outputOption);
        var namespaceResourceName = parseResult.GetValue(bicepNamespaceResourceNameOption) ?? "serviceBusNamespace";
        var serviceBusNamespaceName = parseResult.GetValue(bicepNamespaceNameOption) ?? "sb-flowly";

        var (queueDefinitions, _) = DiscoverQueuesFromAssemblies(assemblies, configurationType, workingDirectory);
        var bicep = AzureServiceBusOutputGenerator.CreateBicepTemplate(namespaceResourceName, serviceBusNamespaceName, queueDefinitions);
        WriteOutput(bicep, output);
    }));

var builderVariableOption = new Option<string>("--builder-variable");
builderVariableOption.Description = "Variable name of DistributedApplicationBuilder in generated code";

var connectionNameOption = new Option<string>("--connection-name");
connectionNameOption.Description = "Connection/resource name passed to AddAzureServiceBus";

var namespaceVariableOption = new Option<string>("--namespace-variable");
namespaceVariableOption.Description = "Variable name assigned to AddAzureServiceBus result";
var aspireCodeCommand = new Command("aspire-code", "Generate C# bootstrap code for Azure Service Bus queue registration in Aspire");
aspireCodeCommand.Add(assemblyOption);
aspireCodeCommand.Add(projectOption);
aspireCodeCommand.Add(configurationOption);
aspireCodeCommand.Add(frameworkOption);
aspireCodeCommand.Add(noBuildOption);
aspireCodeCommand.Add(configurationTypeOption);
aspireCodeCommand.Add(workingDirectoryOption);
aspireCodeCommand.Add(outputOption);
aspireCodeCommand.Add(builderVariableOption);
aspireCodeCommand.Add(connectionNameOption);
aspireCodeCommand.Add(namespaceVariableOption);
aspireCodeCommand.SetAction(parseResult =>
    ExecuteSafely(() =>
    {
        var assemblies = ResolveAssemblies(parseResult, assemblyOption, projectOption, configurationOption, frameworkOption, noBuildOption);
        var configurationType = parseResult.GetValue(configurationTypeOption);
        var workingDirectory = parseResult.GetValue(workingDirectoryOption);
        var output = parseResult.GetValue(outputOption);
        var builderVariable = parseResult.GetValue(builderVariableOption) ?? "builder";
        var connectionName = parseResult.GetValue(connectionNameOption) ?? "EmulatorNamespace";
        var namespaceVariable = parseResult.GetValue(namespaceVariableOption) ?? "azureServiceBus";

        var (queueDefinitions, _) = DiscoverQueuesFromAssemblies(assemblies, configurationType, workingDirectory);
        var code = AzureServiceBusOutputGenerator.CreateAspireBootstrapCode(builderVariable, connectionName, namespaceVariable, queueDefinitions.Select(x => x.Name).ToArray());
        WriteOutput(code, output);
    }));

var shellOption = new Option<string>("--shell");
shellOption.Description = "Shell type for completion script: zsh, bash, or powershell";

var completionCommand = new Command("completion", "Generate shell completion script");
completionCommand.Add(shellOption);
completionCommand.SetAction(parseResult =>
    ExecuteSafely(() =>
    {
        var shell = (parseResult.GetValue(shellOption) ?? "zsh").ToLowerInvariant();
        var script = shell switch
        {
            "zsh" => GetZshCompletionScript(),
            "bash" => GetBashCompletionScript(),
            "powershell" => GetPowerShellCompletionScript(),
            _ => throw new InvalidOperationException("Unsupported shell. Use --shell zsh, --shell bash, or --shell powershell.")
        };

        Console.WriteLine(script);
    }));

var forceOption = new Option<bool>("--force");
forceOption.Description = "Deprecated: completion files are overwritten by default";

var installCompletionCommand = new Command("install-completion", "Install shell completion script to a default location");
installCompletionCommand.Add(shellOption);
installCompletionCommand.Add(forceOption);
installCompletionCommand.SetAction(parseResult =>
    ExecuteSafely(() =>
    {
        var shell = (parseResult.GetValue(shellOption) ?? "zsh").ToLowerInvariant();
        var force = parseResult.GetValue(forceOption);

        var (destinationPath, script, activationHint) = shell switch
        {
            "zsh" =>
            (
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zfunc", "_flowly"),
                GetZshCompletionScript(),
                "Add this to your ~/.zshrc if needed: fpath=(~/.zfunc $fpath) && autoload -Uz compinit && compinit"
            ),
            "bash" =>
            (
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".flowly-completion.bash"),
                GetBashCompletionScript(),
                "Add this to your ~/.bashrc: source ~/.flowly-completion.bash"
            ),
            "powershell" =>
            (
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".flowly-completion.ps1"),
                GetPowerShellCompletionScript(),
                "Add this to your PowerShell profile: . $HOME/.flowly-completion.ps1"
            ),
            _ => throw new InvalidOperationException("Unsupported shell. Use --shell zsh, --shell bash, or --shell powershell.")
        };

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var fileAlreadyExisted = File.Exists(destinationPath);

        File.WriteAllText(destinationPath, script);
        var action = fileAlreadyExisted ? "Updated" : "Installed";
        Console.WriteLine($"{action} {shell} completion script at: {destinationPath}");
        if (force)
        {
            Console.WriteLine("Note: --force is no longer required and can be omitted.");
        }
        Console.WriteLine(activationHint);
    }));

var removeCompletionCommand = new Command("remove-completion", "Remove shell completion script from default location");
removeCompletionCommand.Add(shellOption);
removeCompletionCommand.SetAction(parseResult =>
    ExecuteSafely(() =>
    {
        var shell = (parseResult.GetValue(shellOption) ?? "zsh").ToLowerInvariant();
        var destinationPath = shell switch
        {
            "zsh" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zfunc", "_flowly"),
            "bash" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".flowly-completion.bash"),
            "powershell" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".flowly-completion.ps1"),
            _ => throw new InvalidOperationException("Unsupported shell. Use --shell zsh, --shell bash, or --shell powershell.")
        };

        if (!File.Exists(destinationPath))
        {
            Console.WriteLine($"No completion file found at: {destinationPath}");
            return;
        }

        File.Delete(destinationPath);
        Console.WriteLine($"Removed {shell} completion script from: {destinationPath}");
    }));

azureServiceBusCommand.Add(queuesCommand);
azureServiceBusCommand.Add(emulatorConfigCommand);
azureServiceBusCommand.Add(bicepCommand);
azureServiceBusCommand.Add(aspireCodeCommand);

rootCommand.Add(azureServiceBusCommand);
rootCommand.Add(completionCommand);
rootCommand.Add(installCompletionCommand);
rootCommand.Add(removeCompletionCommand);
return await rootCommand.Parse(args).InvokeAsync();

static FlowlyQueueDiscoveryResult DiscoverQueues(FileInfo assembly, string? configurationType, DirectoryInfo? workingDirectory)
{
    return new FlowlyQueueDiscovery().DiscoverQueues(
        assembly.FullName,
        configurationType,
        workingDirectory?.FullName);
}

static (IReadOnlyList<QueueDiscoveryQueue> QueueDefinitions, IReadOnlyList<string> ConfigurationTypes) DiscoverQueuesFromAssemblies(
    IReadOnlyList<QueueDiscoverySource> sources,
    string? configurationType,
    DirectoryInfo? workingDirectory)
{
    var queueDefinitions = new Dictionary<string, QueueDiscoveryQueue>(StringComparer.OrdinalIgnoreCase);
    var configurationTypes = new SortedSet<string>(StringComparer.Ordinal);
    var failures = new List<string>();

    foreach (var source in sources)
    {
        FlowlyQueueDiscoveryResult result;
        try
        {
            var effectiveWorkingDirectory = workingDirectory ?? source.DefaultWorkingDirectory;
            result = DiscoverQueues(source.Assembly, configurationType, effectiveWorkingDirectory);
        }
        catch (FlowlyConfigurationNotFoundException)
        {
            continue;
        }
        catch (Exception ex)
        {
            failures.Add($"- {source.Assembly.FullName}: {ex.Message}");
            continue;
        }

        configurationTypes.Add(result.ConfigurationType);

        foreach (var queueDefinition in result.QueueDefinitions)
        {
            if (queueDefinitions.TryGetValue(queueDefinition.Name, out var existing))
            {
                if (existing.DefaultMessageTimeToLive != queueDefinition.DefaultMessageTimeToLive)
                {
                    throw new InvalidOperationException($"Conflicting queue setting 'DefaultMessageTimeToLive' for queue '{queueDefinition.Name}'.");
                }

                if (existing.DeadLetterOnMessageExpiration != queueDefinition.DeadLetterOnMessageExpiration)
                {
                    throw new InvalidOperationException($"Conflicting queue setting 'DeadLetterOnMessageExpiration' for queue '{queueDefinition.Name}'.");
                }

                if (existing.LockDuration != queueDefinition.LockDuration)
                {
                    throw new InvalidOperationException($"Conflicting queue setting 'LockDuration' for queue '{queueDefinition.Name}'.");
                }

                queueDefinitions[queueDefinition.Name] = existing with { RequiresSession = existing.RequiresSession || queueDefinition.RequiresSession };

                continue;
            }

            queueDefinitions.Add(queueDefinition.Name, queueDefinition);
        }
    }

    if (failures.Count > 0)
    {
        Console.Error.WriteLine("Warning: Some inputs were skipped during queue discovery:");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine(failure);
        }
    }

    if (queueDefinitions.Count == 0)
    {
        throw new InvalidOperationException(
            "No queues were discovered from provided inputs. " +
            (failures.Count > 0
                ? "All inputs failed queue discovery."
                : "No FlowlyDesignTimeFactory-based configuration was found."));
    }

    var orderedQueueDefinitions = queueDefinitions
        .Values
        .OrderBy(queue => queue.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return (orderedQueueDefinitions, configurationTypes.ToArray());
}

static int ExecuteSafely(Action action)
{
    try
    {
        action();
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
}

static IReadOnlyList<QueueDiscoverySource> ResolveAssemblies(
    ParseResult parseResult,
    Option<string[]> assemblyOption,
    Option<string[]> projectOption,
    Option<string> configurationOption,
    Option<string?> frameworkOption,
    Option<bool> noBuildOption)
{
    var assemblyPaths = parseResult.GetValue(assemblyOption) ?? [];
    var projectPaths = parseResult.GetValue(projectOption) ?? [];

    if (assemblyPaths.Length == 0 && projectPaths.Length == 0)
    {
        throw new InvalidOperationException("At least one --assembly or --project value is required.");
    }

    var configuration = parseResult.GetValue(configurationOption) ?? "Debug";
    var framework = parseResult.GetValue(frameworkOption);
    var noBuild = parseResult.GetValue(noBuildOption);

    var resolvedSources = new List<QueueDiscoverySource>();

    foreach (var assemblyPath in assemblyPaths)
    {
        var assemblyFile = new FileInfo(Path.GetFullPath(assemblyPath));
        var defaultWorkingDirectory = new DirectoryInfo(assemblyFile.DirectoryName ?? Directory.GetCurrentDirectory());
        resolvedSources.Add(new QueueDiscoverySource(assemblyFile, defaultWorkingDirectory));
    }

    foreach (var projectPath in projectPaths)
    {
        var fullProjectPath = Path.GetFullPath(projectPath);
        var projectFile = ResolveProjectFile(fullProjectPath);
        var assemblyFile = ResolveAssemblyFromProjectFile(projectFile, configuration, framework, noBuild);
        var defaultWorkingDirectory = new DirectoryInfo(Path.GetDirectoryName(projectFile) ?? Directory.GetCurrentDirectory());
        resolvedSources.Add(new QueueDiscoverySource(assemblyFile, defaultWorkingDirectory));
    }

    return resolvedSources
        .GroupBy(source => source.Assembly.FullName, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .ToArray();
}

static FileInfo ResolveAssemblyFromProjectFile(string projectFile, string configuration, string? framework, bool noBuild)
{
    if (!noBuild)
    {
        var buildArgs = $"build \"{projectFile}\" -c {configuration}";
        if (!string.IsNullOrWhiteSpace(framework))
        {
            buildArgs += $" -f {framework}";
        }

        RunDotNet(buildArgs, Path.GetDirectoryName(projectFile)!);
    }

    var msbuildArgs = $"msbuild \"{projectFile}\" -nologo -getProperty:TargetPath -property:Configuration={configuration}";
    if (!string.IsNullOrWhiteSpace(framework))
    {
        msbuildArgs += $" -property:TargetFramework={framework}";
    }

    var output = RunDotNet(msbuildArgs, Path.GetDirectoryName(projectFile)!);
    var targetPath = output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .LastOrDefault(line => line.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));

    if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
    {
        throw new InvalidOperationException(
            "Could not resolve compiled assembly path from project. If project is multi-targeted, pass --framework.");
    }

    return new FileInfo(targetPath);
}

static string ResolveProjectFile(string fullPath)
{
    if (File.Exists(fullPath))
    {
        if (Path.GetExtension(fullPath).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        throw new InvalidOperationException("--project must point to a .csproj file or a folder containing one .csproj file.");
    }

    if (!Directory.Exists(fullPath))
    {
        throw new InvalidOperationException($"Project path was not found: {fullPath}");
    }

    var csprojFiles = Directory.GetFiles(fullPath, "*.csproj", SearchOption.TopDirectoryOnly);
    return csprojFiles.Length switch
    {
        0 => throw new InvalidOperationException("No .csproj file was found in the project folder."),
        > 1 => throw new InvalidOperationException("Multiple .csproj files found. Pass the .csproj path explicitly."),
        _ => csprojFiles[0]
    };
}

static string RunDotNet(string arguments, string workingDirectory)
{
    var processStartInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = arguments,
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    using var process = Process.Start(processStartInfo)
        ?? throw new InvalidOperationException("Could not start dotnet process.");

    var stdOut = process.StandardOutput.ReadToEnd();
    var stdErr = process.StandardError.ReadToEnd();

    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"dotnet {arguments} failed with exit code {process.ExitCode}.{Environment.NewLine}{stdErr}{stdOut}");
    }

    return string.IsNullOrWhiteSpace(stdOut) ? stdErr : stdOut;
}

static string GetZshCompletionScript()
{
    return """
#compdef flowly

_flowly() {
  local -a commands
  commands=(
    'azure-service-bus:Azure Service Bus specific commands'
    'completion:Generate shell completion script'
        'install-completion:Install shell completion script to a default location'
        'remove-completion:Remove shell completion script from default location'
  )

  if (( CURRENT == 2 )); then
    _describe 'command' commands
    return
  fi

  case "$words[2]" in
    azure-service-bus)
      local -a asb_commands
      asb_commands=(
        'queues:Discover and print all Flowly queue names from one or more assemblies/projects'
        'emulator-config:Generate Azure Service Bus emulator config JSON'
        'bicep:Generate Bicep template for Azure Service Bus queues'
        'aspire-code:Generate C# bootstrap code for Azure Service Bus queue registration in Aspire'
      )

      if (( CURRENT == 3 )); then
        _describe 'azure-service-bus command' asb_commands
        return
      fi

      _arguments -S \
        '--assembly[Path to compiled assembly]:assembly file:_files' \
        '--project[Path to .csproj or folder]:project:_files -/' \
        '--configuration[Build configuration]:configuration:(Debug Release)' \
        '--framework[Target framework]:framework:' \
        '--no-build[Do not build project]' \
        '--configuration-type[Flowly configuration type]:type:' \
        '--working-directory[Working directory]:directory:_files -/' \
        '--output[Output file]:output file:_files' \
        '--namespace[Service Bus namespace]:namespace:' \
        '--namespace-resource-name[Bicep namespace resource symbol]:symbol:' \
        '--service-bus-namespace-name[Service Bus namespace name]:name:' \
        '--builder-variable[Builder variable name]:name:' \
        '--connection-name[AddAzureServiceBus connection name]:name:' \
        '--namespace-variable[Namespace variable name]:name:'
      ;;

    completion)
            _arguments '--shell[Shell type]:shell:(zsh bash powershell)'
            ;;

        install-completion)
            _arguments '--shell[Shell type]:shell:(zsh bash powershell)' '--force[Overwrite completion file if it already exists]'
            ;;

        remove-completion)
            _arguments '--shell[Shell type]:shell:(zsh bash powershell)'
      ;;
  esac
}

_flowly "$@"
""";
}

static string GetBashCompletionScript()
{
    return """
_flowly()
{
    local cur prev
    COMPREPLY=()
    cur="${COMP_WORDS[COMP_CWORD]}"
    prev="${COMP_WORDS[COMP_CWORD-1]}"

    if [[ ${COMP_CWORD} -eq 1 ]]; then
        COMPREPLY=( $(compgen -W "azure-service-bus completion install-completion remove-completion" -- "$cur") )
        return 0
    fi

    if [[ ${COMP_WORDS[1]} == "azure-service-bus" && ${COMP_CWORD} -eq 2 ]]; then
        COMPREPLY=( $(compgen -W "queues emulator-config bicep aspire-code" -- "$cur") )
        return 0
    fi

    if [[ ${COMP_WORDS[1]} == "completion" ]]; then
        COMPREPLY=( $(compgen -W "--shell zsh bash powershell" -- "$cur") )
        return 0
    fi

    if [[ ${COMP_WORDS[1]} == "install-completion" ]]; then
        COMPREPLY=( $(compgen -W "--shell --force zsh bash powershell" -- "$cur") )
        return 0
    fi

    if [[ ${COMP_WORDS[1]} == "remove-completion" ]]; then
        COMPREPLY=( $(compgen -W "--shell zsh bash powershell" -- "$cur") )
        return 0
    fi

    COMPREPLY=( $(compgen -W "--assembly --project --configuration --framework --no-build --configuration-type --working-directory --output --namespace --namespace-resource-name --service-bus-namespace-name --builder-variable --connection-name --namespace-variable" -- "$cur") )
    return 0
}

complete -F _flowly flowly
""";
}

static string GetPowerShellCompletionScript()
{
    return """
Register-ArgumentCompleter -CommandName flowly -ScriptBlock {
    param($commandName, $wordToComplete, $cursorPosition, $commandAst, $fakeBoundParameters)

    $tokens = @($commandAst.CommandElements | Select-Object -Skip 1 | ForEach-Object { $_.Value })

    $rootCommands = @('azure-service-bus', 'completion', 'install-completion', 'remove-completion')
    $asbCommands = @('queues', 'emulator-config', 'bicep', 'aspire-code')

    if ($tokens.Count -eq 0) {
        $rootCommands | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
        }
        return
    }

    if ($tokens[0] -eq 'azure-service-bus' -and $tokens.Count -le 1) {
        $asbCommands | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
        }
        return
    }

    $options = @(
        '--assembly', '--project', '--configuration', '--framework', '--no-build',
        '--configuration-type', '--working-directory', '--output', '--namespace',
        '--namespace-resource-name', '--service-bus-namespace-name', '--builder-variable',
        '--connection-name', '--namespace-variable', '--shell', '--force'
    )

    $options | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterName', $_)
    }
}
""";
}

static void WriteOutput(string content, FileInfo? output)
{
    if (output is null)
    {
        Console.WriteLine(content);
        return;
    }

    var outputPath = Path.GetFullPath(output.FullName);
    var outputDirectory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }

    File.WriteAllText(outputPath, content);
    Console.WriteLine($"Wrote output to: {outputPath}");
}
