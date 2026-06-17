# Skills Conventions

Claude Code skills live in `.claude/skills/`. Each skill is a directory containing a `SKILL.md` file.

## Skills are deployed to user sites — they must be self-contained

Skills are synced into `src/Flowly.Templates/content/flowly-skills/` via the `SyncSkills` MSBuild target and shipped as part of the `Flowly.Templates` NuGet package. When a user runs `dotnet new flowlyskills`, only `.claude/skills/` lands in their project — nothing from `.claude/rules/`, `docs/`, or anywhere else in this repo.

Consequences for authoring:

- **Do not reference repo-local files.** A skill step must not cite or rely on paths like `.claude/rules/*.md`, `Flowly.Tool/Flowly.Tool.csproj`, or any file that only exists in the Flowly source repository.
- **Embed all instructions inline.** Every instruction a skill needs to give must be written directly in the `SKILL.md` — no "see `rules/foo.md` for details."
- **Install CLI tools from NuGet, not from source.** User sites do not have the Flowly source code. Any skill step that uses the `flowly` CLI must install it with `dotnet tool install --global Flowly.Tool`, not by building from `Flowly.Tool/Flowly.Tool.csproj`.

## NuGet references — always state them explicitly in the skill

Skills that instruct the agent to add a NuGet package must include the **exact** `<PackageReference>` line, registration method name, and connection string key directly in the `SKILL.md`. Do not leave the agent to discover these from package contents, the NuGet Gallery, or any other external source.

Concretely: every `<PackageReference>` a skill requires must appear verbatim in that skill, typically as a table mapping a user choice to the exact XML element. If a skill says "add the backend package" without naming it, that is a gap — fix it.

## Build verification — every skill must verify the build

Every skill that makes code changes must end with a build verification step and include it in the checklist. Embed this directly — do not reference this rules file:

```bash
dotnet build
```

The step must appear as the last action before reporting completion. If the build fails the agent must fix the errors and rebuild before finishing.

## Keeping skills current

Skills must be updated in the same change as the code they describe. When implementing a feature, ask: *does this affect what a developer needs to know to use Flowly correctly from an AI assistant?* If yes, update the relevant skill before considering the task done.

Common triggers:

- A new registration method is added (e.g. a new transport or backend package) → update `flowly-setup-azure-service-bus/SKILL.md` and any other setup skills
- Handler base classes gain new capabilities or new configuration options → update `create-message-handler/SKILL.md`
- `RecurringJobHandler` or `[RecurringJob]` behaviour changes → update `create-recurring-job/SKILL.md`
- The contracts project pattern or `IJobMessage` contract changes → update `create-contracts-assembly/SKILL.md`
- `Flowly.OpenTelemetry` registration pattern changes in the `flowly-project` or `flowlyaspireapp` templates → update `add-opentelemetry/SKILL.md` so the wiring stays consistent
- A previously documented step becomes wrong, redundant, or replaced by a better approach → fix or remove it

## Adding new skills

When adding a significant new feature that a developer would scaffold repeatedly, consider adding a skill. Follow the existing structure: one directory per skill, `SKILL.md` inside, frontmatter with `description` and `arguments` if the skill accepts parameters.

Naming convention:
- Generic skills (applicable to any project using Flowly): flat name, e.g. `create-message-handler`
- Flowly-specific or library-specific skills: prefixed with `flowly-`, e.g. `flowly-setup-azure-service-bus`

## Flowly CLI tool — always install before use

See `flowly-tool-conventions.md` for the full convention. The short rule: embed this check inline immediately before the first `flowly` command in any skill step — never reference the external rule file, since skills are deployed to user sites that don't have this repository:

```bash
dotnet tool list --global | grep -q "flowly.tool" || dotnet tool install --global Flowly.Tool
```

If the command fails after install, run `dotnet tool update --global Flowly.Tool` and retry. Never reimplement what the tool does — always install it instead.

## Transport-specific behaviors skills must encode

### Azure Service Bus emulator — no topology creation

The Azure Service Bus emulator **does not support dynamic topology creation**. Any skill that scaffolds a new ASB project or adds a new handler to an existing ASB project must account for this:

1. **New project scaffolded via `dotnet new flowly --transport azureservicebus`:** the template does not set `CreateTopology = false`. The skill must patch `Program.cs` after scaffolding:

   ```csharp
   // Change this:
   builder.AddFlowly<FlowlyConfiguration>();

   // To this:
   builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
   ```

   For inline wiring, add `options => options.CreateTopology = false` as the first lambda argument to `builder.AddFlowly`.

2. **New queues introduced (new project or new handler):** regenerate `sbconfig.json` so the emulator knows about all queues before startup:

   ```bash
   flowly azure-service-bus emulator-config \
     --project ./<Project1> \
     --project ./<Project2> \
     --namespace EmulatorNamespace \
     --output ./sbconfig.json
   ```

   Pass `--project` for every project in the solution that has a `FlowlyConfiguration`. Tell the user to verify `--namespace` matches their `docker-compose.yml`.

These two steps apply even for handlers that use Flowly-internal execution lanes (e.g. `RecurringJobHandler`), because the lane queues must also be declared in `sbconfig.json`.
