---
paths:
  - ".claude/skills/**"
---

# Skills Conventions

Claude Code skills live in `.claude/skills/`: one directory per skill containing a `SKILL.md`.

## Skills are deployed to user sites — they must be self-contained

Skills are synced into `src/Flowly.Templates/content/flowly-skills/` by the `SyncSkills` MSBuild target and shipped in the `Flowly.Templates` NuGet package. `dotnet new flowlyskills` lands only `.claude/skills/` in a user's project — nothing from `.claude/rules/`, `docs/` or elsewhere in this repo. Therefore:

- **Do not reference repo-local files** (`.claude/rules/*.md`, `Flowly.Tool/Flowly.Tool.csproj`, ...).
- **Embed every instruction inline** in the `SKILL.md`; no "see `rules/foo.md`".
- **Install CLI tools from NuGet, not source:** `dotnet tool install --global Flowly.Tool`.

## NuGet references — state them explicitly

A skill that adds a NuGet package must include the **exact** `<PackageReference>` line, registration method name and connection string key, typically as a table mapping a user choice to the XML element. Never leave the agent to discover them from package contents or the NuGet Gallery; "add the backend package" without naming it is a gap.

## Build verification

Every skill that changes code must end with a build verification step, included in its checklist and as the last action before reporting completion (embed it inline):

```bash
dotnet build
```

If the build fails, fix the errors and rebuild before finishing.

## Keeping skills current

Update skills in the same change as the code they describe: ask whether the change affects what a developer needs to know to use Flowly from an AI assistant. Common triggers:

- new registration method (transport or backend package) → `flowly-setup-azure-service-bus/SKILL.md` and the other setup skills
- new handler base-class capabilities or options → `create-message-handler/SKILL.md`
- `HandlerStep` / `SendStep` API, registration or semantics changes → `create-handler-step/SKILL.md`
- `RecurringJobHandler` / `[RecurringJob]` changes → `create-recurring-job/SKILL.md`
- contracts project pattern or `IJobMessage` changes → `create-contracts-assembly/SKILL.md`
- `Flowly.OpenTelemetry` registration pattern changes in the `flowly-project` or `flowlyaspireapp` templates → `add-opentelemetry/SKILL.md`
- `MessageStreamHandler<T>` options or `[StreamRetention]` changes → `create-message-stream-handler/SKILL.md`
- any previously documented step that became wrong, redundant or replaced → fix or remove it

## Setup skills — prefer Flowly.Templates

Skills that set up a new Flowly project or solution use the templates as the **primary path**, not manual package and configuration steps (templates are tested and version-consistent; reimplementing their output creates drift).

Structure of every setup skill:

1. Ask upfront: **new project** or **existing project**?
2. **New project** — invoke the template, list relevant flags, add post-scaffold notes (e.g. `dotnet sln add`, `CreateTopology = false` for ASB), then point to the handler/job skills.
3. **Existing project** — manual steps (NuGet packages, `FlowlyConfiguration`, `Program.cs`) only for projects not created from a template.

| Scenario | Template command |
|---|---|
| New complete solution (Messages + Sender + Receiver) | `dotnet new flowlyapp --transport <x>` |
| New Aspire solution | `dotnet new flowlyaspireapp --transport <x>` |
| New single project in an existing solution | `dotnet new flowly --transport <x>`, then `dotnet sln add` |

When a template's flags, generated layout or default wiring change, update the setup skill's template step in the same change; the manual path too if package names or registration calls changed.

## Adding new skills

Consider a skill for a significant feature a developer would scaffold repeatedly. One directory per skill, `SKILL.md` with frontmatter `description` (and `arguments` if it takes parameters). Generic skills use a flat name (`create-message-handler`); Flowly- or library-specific ones are prefixed `flowly-` (`flowly-setup-azure-service-bus`).

## Flowly CLI tool — always install before use

Embed this check inline immediately before the first `flowly` command in any skill step (never reference an external rule file):

```bash
dotnet tool list --global | grep -q "flowly.tool" || dotnet tool install --global Flowly.Tool
```

If the command still fails, run `dotnet tool update --global Flowly.Tool` and retry once. Never reimplement what the tool does (e.g. hand-writing `sbconfig.json`).

## Azure Service Bus emulator — no topology creation

The emulator **does not support dynamic topology creation**. Any skill that scaffolds a new ASB project or adds a handler to an existing one must:

1. **Patch `Program.cs` after `dotnet new flowly --transport azureservicebus`** (the template does not set `CreateTopology = false`):

   ```csharp
   // Change this:
   builder.AddFlowly<FlowlyConfiguration>();

   // To this:
   builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
   ```

   For inline wiring, pass `options => options.CreateTopology = false` as the first lambda argument to `builder.AddFlowly`.

2. **Regenerate `sbconfig.json` whenever queues are introduced** (new project or handler):

   ```bash
   flowly azure-service-bus emulator-config \
     --project ./<Project1> \
     --project ./<Project2> \
     --namespace EmulatorNamespace \
     --output ./sbconfig.json
   ```

   Pass `--project` for every project in the solution that has a `FlowlyConfiguration`, and tell the user to verify `--namespace` matches their `docker-compose.yml`.

Both steps apply even to handlers using Flowly-internal execution lanes (e.g. `RecurringJobHandler`), since the lane queues must be declared in `sbconfig.json` too.
