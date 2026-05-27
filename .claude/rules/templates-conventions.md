# Flowly.Templates Conventions

## Keeping template documentation current

Whenever `src/Flowly.Templates/` changes, update **all** of the following in the same change:

1. `src/Flowly.Templates/README.md` — the NuGet package readme; the authoritative reference for template usage
2. `README.md` (root) — the Project Templates section
3. `docs/ai/CONTEXT.md` — section 12, Project Templates
4. `.claude/CLAUDE.md` — the `Flowly.Templates/` entry in the Project Structure list

The task is **not done** until all four are consistent.

## Template inventory

The package ships four templates:

| Short name | Command | Purpose |
|---|---|---|
| `flowly` | `dotnet new flowly --transport <value> [options]` | Scaffold a new Flowly ASP.NET Core project |
| `flowlymessagelib` | `dotnet new flowlymessagelib [--jobs] [options]` | Scaffold a Flowly message contracts class library |
| `flowlyskills` | `dotnet new flowlyskills` | Install Flowly Claude Code AI skills into `.claude/skills/` |
| `flowlydocker` | `dotnet new flowlydocker [options]` | Generate a Docker Compose file with local infrastructure |

## `flowly` template parameters

`--transport <value>` is **required**. Accepted values and aliases:

| Value | Alias | Transport |
|---|---|---|
| `rabbitmq` | `rmq` | RabbitMQ |
| `azureservicebus` | `asb` | Azure Service Bus |
| `inmemory` | `inm` | In-Memory (no broker) |

Transport is a single `--transport` parameter, **not** individual boolean flags. Do not document or generate commands like `--rabbitmq` or `--AzureServiceBus`.

Optional feature flags: `--jobtracking` / `--jobs`, `--deadlettertracking` / `--deadletter`, `--opentelemetry` / `--otel`, `--inline`.

Database backend flags (required when jobs or dead-letter tracking is enabled): `--sqlserver`, `--postgres`, `--sqlite`.

## SyncSkills MSBuild target

The `SyncSkills` target in `Flowly.Templates.csproj` copies `.claude/skills/**` into `content/flowly-skills/.claude/skills/` at build time. Skills are therefore not committed directly under `content/flowly-skills/` — they are generated. Do not manually edit files under `content/flowly-skills/`; edit the source under `.claude/skills/` instead.

## Adding or removing a template parameter

When a parameter is added, removed, or renamed in `content/flowly-project/.template.config/template.json`:

1. Update `src/Flowly.Templates/README.md` first — it is the source of truth
2. Propagate to `README.md`, `docs/ai/CONTEXT.md`, and `.claude/CLAUDE.md`
3. Update any skills in `.claude/skills/` that reference template usage
