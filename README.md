# Flowly

Messaging abstraction for queue based communication. Please see /docs folder for details.

## Build

dotnet build

## Test

dotnet test

## Flowly.Tool (.NET tool)

`Flowly.Tool` is packaged as a .NET CLI tool and exposes the command `dotnet flowly`.

### Pack and install locally

```bash
dotnet pack Flowly.Tool/Flowly.Tool.csproj -c Release
dotnet tool install --global --add-source ./Flowly.Tool/bin/Release Flowly.Tool
```

For updating an existing install:

```bash
dotnet tool update --global --add-source ./Flowly.Tool/bin/Release Flowly.Tool
```

### Command structure

The tool is provider-scoped to keep room for future providers:

```bash
dotnet flowly azure-service-bus <command> [options]
```

You can now provide either a compiled assembly OR a project path/folder:

- `--assembly` / `-a` points directly to a `.dll`
- `--project` / `-p` points to a `.csproj` file or a folder containing one `.csproj`
- with `--project`, the tool resolves target assembly similar to `dotnet-ef`
- both `--assembly` and `--project` can be specified multiple times to combine queues from multiple components

### Azure Service Bus commands

Discover queues from a `FlowlyDesignTimeFactory` + `IFlowlyConfiguration` implementation in an assembly:

```bash
dotnet flowly azure-service-bus queues \
	--assembly BackendProcessor/bin/Debug/net10.0/BackendProcessor.dll \
	--working-directory BackendProcessor
```

Discover queues by pointing to project folder (no dll path needed):

```bash
dotnet flowly azure-service-bus queues \
	--project ./BackendProcessor \
	--configuration Debug \
	--framework net10.0 \
	--working-directory BackendProcessor
```

Discover queues by combining multiple projects:

```bash
dotnet flowly azure-service-bus queues \
	--project ./BackendProcessor \
	--project ./SomeOtherProcessor \
	--framework net10.0
```

When multiple inputs are used, each `--project` uses its own project folder as working directory by default. Use `--working-directory` only when you explicitly want to override that for all inputs.

Generate Azure Service Bus Emulator config JSON:

```bash
dotnet flowly azure-service-bus emulator-config \
	--assembly BackendProcessor/bin/Debug/net10.0/BackendProcessor.dll \
	--working-directory BackendProcessor \
	--namespace EmulatorNamespace \
	--output ./artifacts/servicebus-emulator-config.json
```

Generate Bicep for Service Bus queues:

```bash
dotnet flowly azure-service-bus bicep \
	--assembly BackendProcessor/bin/Debug/net10.0/BackendProcessor.dll \
	--working-directory BackendProcessor \
	--service-bus-namespace-name sb-flowly \
	--output ./artifacts/servicebus-queues.bicep
```

Generate Aspire bootstrap C# for queue setup:

```bash
dotnet flowly azure-service-bus aspire-code \
	--assembly BackendProcessor/bin/Debug/net10.0/BackendProcessor.dll \
	--working-directory BackendProcessor \
	--connection-name EmulatorNamespace \
	--output ./artifacts/servicebus-aspire-bootstrap.cs
```

Shared options:

- `--assembly` / `-a` (use this or `--project`)
- `--project` / `-p` (use this or `--assembly`)
- `--assembly` and `--project` are repeatable for multi-component queue aggregation
- `--configuration` (default: `Debug`, when using `--project`)
- `--framework` (optional, recommended for multi-target projects)
- `--no-build` (skip build when using `--project`)
- `--configuration-type` / `-t` (optional when exactly one candidate exists)
- `--working-directory` / `-w` (defaults to assembly directory)
- `--output` / `-o` (optional for generators; stdout if omitted)

### Shell completion

Generate and install zsh completion:

```bash
dotnet flowly completion --shell zsh > ~/.zfunc/_flowly
```

Or let the tool install it directly:

```bash
dotnet flowly install-completion --shell zsh
```

Running `install-completion` again updates the existing completion file.

Remove it again:

```bash
dotnet flowly remove-completion --shell zsh
```

Generate and install bash completion:

```bash
dotnet flowly completion --shell bash > ~/.flowly-completion.bash
```

Or let the tool install it directly:

```bash
dotnet flowly install-completion --shell bash
```

Running `install-completion` again updates the existing completion file.

Remove it again:

```bash
dotnet flowly remove-completion --shell bash
```

PowerShell (Windows/macOS/Linux):

```powershell
dotnet flowly install-completion --shell powershell
dotnet flowly remove-completion --shell powershell
```

Running `install-completion` again updates the existing completion file.

Note: `oh-my-posh` only customizes your prompt theme. Completion support depends on shell integration (zsh/bash/PowerShell), so it works fine with `oh-my-posh` as long as your shell profile loads the generated completion script.