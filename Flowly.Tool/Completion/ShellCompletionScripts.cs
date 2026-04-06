namespace Flowly.Tool.Completion;

internal static class ShellCompletionScripts
{
    public static string GetZsh() => """
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

              local -a common_opts
              common_opts=(
                '--assembly[Path to compiled assembly]:assembly file:_files'
                '--project[Path to .csproj or folder]:project:_files -/'
                '--configuration[Build configuration]:configuration:(Debug Release)'
                '--framework[Target framework]:framework:'
                '--no-build[Do not build project]'
                '--configuration-type[Flowly configuration type]:type:'
                '--working-directory[Working directory]:directory:_files -/'
              )

              case "$words[3]" in
                queues)
                  _arguments "${common_opts[@]}"
                  ;;
                emulator-config)
                  _arguments "${common_opts[@]}" \
                    '--output[Output file]:output file:_files' \
                    '--namespace[Service Bus namespace]:namespace:'
                  ;;
                bicep)
                  _arguments "${common_opts[@]}" \
                    '--output[Output file]:output file:_files' \
                    '--namespace-resource-name[Bicep namespace resource symbol]:symbol:' \
                    '--service-bus-namespace-name[Service Bus namespace name]:name:'
                  ;;
                aspire-code)
                  _arguments "${common_opts[@]}" \
                    '--output[Output file]:output file:_files' \
                    '--builder-variable[Builder variable name]:name:' \
                    '--connection-name[AddAzureServiceBus connection name]:name:' \
                    '--namespace-variable[Namespace variable name]:name:'
                  ;;
                *)
                  _arguments "${common_opts[@]}"
                  ;;
              esac
              ;;

            completion|install-completion|remove-completion)
              _arguments '--shell[Shell type]:shell:(zsh bash powershell)'
              ;;
          esac
        }

        _flowly "$@"
        """;

    public static string GetBash() => """
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

            if [[ ${COMP_WORDS[1]} == "azure-service-bus" && ${COMP_CWORD} -gt 2 ]]; then
                local common="--assembly --project --configuration --framework --no-build --configuration-type --working-directory"
                case "${COMP_WORDS[2]}" in
                    queues)
                        COMPREPLY=( $(compgen -W "$common" -- "$cur") )
                        ;;
                    emulator-config)
                        COMPREPLY=( $(compgen -W "$common --output --namespace" -- "$cur") )
                        ;;
                    bicep)
                        COMPREPLY=( $(compgen -W "$common --output --namespace-resource-name --service-bus-namespace-name" -- "$cur") )
                        ;;
                    aspire-code)
                        COMPREPLY=( $(compgen -W "$common --output --builder-variable --connection-name --namespace-variable" -- "$cur") )
                        ;;
                    *)
                        COMPREPLY=( $(compgen -W "$common --output" -- "$cur") )
                        ;;
                esac
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
        }

        complete -F _flowly flowly
        """;

    public static string GetPowerShell() => """
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

            if ($tokens[0] -eq 'azure-service-bus') {
                $subcommand = if ($tokens.Count -ge 2 -and $tokens[1] -in $asbCommands) { $tokens[1] } else { $null }

                if ($null -eq $subcommand) {
                    $asbCommands | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
                        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
                    }
                    return
                }

                $common = @('--assembly', '--project', '--configuration', '--framework', '--no-build', '--configuration-type', '--working-directory')
                $options = $common
                if ($subcommand -eq 'emulator-config') {
                    $options = $common + @('--output', '--namespace')
                } elseif ($subcommand -eq 'bicep') {
                    $options = $common + @('--output', '--namespace-resource-name', '--service-bus-namespace-name')
                } elseif ($subcommand -eq 'aspire-code') {
                    $options = $common + @('--output', '--builder-variable', '--connection-name', '--namespace-variable')
                }

                $options | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
                    [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterName', $_)
                }
                return
            }

            if ($tokens[0] -in @('completion', 'install-completion', 'remove-completion')) {
                @('--shell', 'zsh', 'bash', 'powershell') | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
                    [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
                }
            }
        }
        """;
}
