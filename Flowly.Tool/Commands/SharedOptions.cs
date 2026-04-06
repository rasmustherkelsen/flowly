using System.CommandLine;

namespace Flowly.Tool.Commands;

internal sealed class SharedOptions
{
    public SharedOptions()
    {
        Assembly = new Option<string[]>("--assembly", ["-a"]) { Required = false };
        Assembly.Description = "Path to a compiled assembly containing Flowly configuration (can be specified multiple times)";

        Project = new Option<string[]>("--project", ["-p"]);
        Project.Description = "Path to a .csproj file or a folder containing a single .csproj (can be specified multiple times)";

        Configuration = new Option<string>("--configuration");
        Configuration.Description = "Build configuration used when --project is provided";

        Framework = new Option<string?>("--framework");
        Framework.Description = "Target framework used when --project is provided (for multi-target projects)";

        NoBuild = new Option<bool>("--no-build");
        NoBuild.Description = "Do not build the project before resolving target assembly";

        ConfigurationType = new Option<string?>("--configuration-type", ["-t"]);
        ConfigurationType.Description = "Full type name or short type name of the FlowlyDesignTimeFactory-derived configuration class";

        WorkingDirectory = new Option<DirectoryInfo?>("--working-directory", ["-w"]);
        WorkingDirectory.Description = "Working directory used when running configuration (defaults to assembly directory)";

        Output = new Option<FileInfo?>("--output", ["-o"]);
        Output.Description = "Optional output file. If omitted, content is written to stdout";
    }

    public Option<string[]> Assembly { get; }
    public Option<string[]> Project { get; }
    public Option<string> Configuration { get; }
    public Option<string?> Framework { get; }
    public Option<bool> NoBuild { get; }
    public Option<string?> ConfigurationType { get; }
    public Option<DirectoryInfo?> WorkingDirectory { get; }
    public Option<FileInfo?> Output { get; }
}
