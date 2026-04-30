using System.CommandLine;
using Flowly.Tool.Completion;

namespace Flowly.Tool.Commands;

internal static class CompletionCommands
{
    private static readonly Option<string> ShellOption = CreateShellOption();

    private static Option<string> CreateShellOption()
    {
        var option = new Option<string>("--shell");
        option.Description = "Shell type for completion script: zsh, bash, or powershell";
        return option;
    }

    public static Command BuildCompletion()
    {
        var command = new Command("completion", "Generate shell completion script");
        command.Add(ShellOption);
        command.SetAction(parseResult => CommandExecutor.ExecuteSafely(() =>
        {
            var shell = (parseResult.GetValue(ShellOption) ?? "zsh").ToLowerInvariant();
            var script = ResolveScript(shell);
            Console.WriteLine(script);
        }));

        return command;
    }

    public static Command BuildInstallCompletion()
    {
        var forceOption = new Option<bool>("--force");
        forceOption.Description = "Deprecated: completion files are overwritten by default";

        var command = new Command("install-completion", "Install shell completion script to a default location");
        command.Add(ShellOption);
        command.Add(forceOption);
        command.SetAction(parseResult => CommandExecutor.ExecuteSafely(() =>
        {
            var shell = (parseResult.GetValue(ShellOption) ?? "zsh").ToLowerInvariant();
            var force = parseResult.GetValue(forceOption);

            var (destinationPath, script, activationHint) = ResolveInstallTarget(shell);

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

        return command;
    }

    public static Command BuildRemoveCompletion()
    {
        var command = new Command("remove-completion", "Remove shell completion script from default location");
        command.Add(ShellOption);
        command.SetAction(parseResult => CommandExecutor.ExecuteSafely(() =>
        {
            var shell = (parseResult.GetValue(ShellOption) ?? "zsh").ToLowerInvariant();
            var destinationPath = ResolveInstallPath(shell);

            if (!File.Exists(destinationPath))
            {
                Console.WriteLine($"No completion file found at: {destinationPath}");
                return;
            }

            File.Delete(destinationPath);
            Console.WriteLine($"Removed {shell} completion script from: {destinationPath}");
        }));

        return command;
    }

    private static string ResolveScript(string shell) => shell switch
    {
        "zsh" => ShellCompletionScripts.GetZsh(),
        "bash" => ShellCompletionScripts.GetBash(),
        "powershell" => ShellCompletionScripts.GetPowerShell(),
        _ => throw new InvalidOperationException("Unsupported shell. Use --shell zsh, --shell bash, or --shell powershell.")
    };

    private static string ResolveInstallPath(string shell) => shell switch
    {
        "zsh" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zfunc", "_flowly"),
        "bash" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".flowly-completion.bash"),
        "powershell" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".flowly-completion.ps1"),
        _ => throw new InvalidOperationException("Unsupported shell. Use --shell zsh, --shell bash, or --shell powershell.")
    };

    private static (string DestinationPath, string Script, string ActivationHint) ResolveInstallTarget(string shell) => shell switch
    {
        "zsh" => (
            ResolveInstallPath("zsh"),
            ShellCompletionScripts.GetZsh(),
            "Add this to your ~/.zshrc if needed: fpath=(~/.zfunc $fpath) && autoload -Uz compinit && compinit"
        ),
        "bash" => (
            ResolveInstallPath("bash"),
            ShellCompletionScripts.GetBash(),
            "Add this to your ~/.bashrc: source ~/.flowly-completion.bash"
        ),
        "powershell" => (
            ResolveInstallPath("powershell"),
            ShellCompletionScripts.GetPowerShell(),
            "Add this to your PowerShell profile: . $HOME/.flowly-completion.ps1"
        ),
        _ => throw new InvalidOperationException("Unsupported shell. Use --shell zsh, --shell bash, or --shell powershell.")
    };
}
