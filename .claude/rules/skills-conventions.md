# Skills Conventions

Claude Code skills live in `.claude/skills/`. Each skill is a directory containing a `SKILL.md` file.

## Keeping skills current

Skills must be updated in the same change as the code they describe. When implementing a feature, ask: *does this affect what a developer needs to know to use Flowly correctly from an AI assistant?* If yes, update the relevant skill before considering the task done.

Common triggers:

- A new registration method is added (e.g. a new transport or backend package) → update `flowly-setup-azure-service-bus/SKILL.md` and any other setup skills
- Handler base classes gain new capabilities or new configuration options → update `create-message-handler/SKILL.md`
- `RecurringJobHandler` or `[RecurringJob]` behaviour changes → update `create-recurring-job/SKILL.md`
- The contracts project pattern or `IJobMessage` contract changes → update `create-contracts-assembly/SKILL.md`
- A previously documented step becomes wrong, redundant, or replaced by a better approach → fix or remove it

## Adding new skills

When adding a significant new feature that a developer would scaffold repeatedly, consider adding a skill. Follow the existing structure: one directory per skill, `SKILL.md` inside, frontmatter with `description` and `arguments` if the skill accepts parameters.

Naming convention:
- Generic skills (applicable to any project using Flowly): flat name, e.g. `create-message-handler`
- Flowly-specific or library-specific skills: prefixed with `flowly-`, e.g. `flowly-setup-azure-service-bus`

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
