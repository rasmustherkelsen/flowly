using System.CommandLine;
using System.Diagnostics;
using Flowly.Tool.Commands;
using Flowly.Tool.QueueDiscovery;

namespace Flowly.Tool.ProjectResolution;

internal static class ProjectAssemblyResolver
{
    public static IReadOnlyList<QueueDiscoverySource> Resolve(ParseResult parseResult, SharedOptions options)
    {
        var assemblyPaths = parseResult.GetValue(options.Assembly) ?? [];
        var projectPaths = parseResult.GetValue(options.Project) ?? [];

        if (assemblyPaths.Length == 0 && projectPaths.Length == 0)
        {
            throw new InvalidOperationException("At least one --assembly or --project value is required.");
        }

        var configuration = parseResult.GetValue(options.Configuration) ?? "Debug";
        var framework = parseResult.GetValue(options.Framework);
        var noBuild = parseResult.GetValue(options.NoBuild);

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

    private static FileInfo ResolveAssemblyFromProjectFile(string projectFile, string configuration, string? framework, bool noBuild)
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

    private static string ResolveProjectFile(string fullPath)
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

    private static string RunDotNet(string arguments, string workingDirectory)
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
            throw new InvalidOperationException(
                $"dotnet {arguments} failed with exit code {process.ExitCode}.{Environment.NewLine}{stdErr}{stdOut}");
        }

        return string.IsNullOrWhiteSpace(stdOut) ? stdErr : stdOut;
    }
}
