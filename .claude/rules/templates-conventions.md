---
paths:
  - "src/Flowly.Templates/**"
---

# Flowly.Templates Conventions

## Keeping template documentation current

Whenever `src/Flowly.Templates/` changes, update **all** of these in the same change:

1. `src/Flowly.Templates/README.md` — NuGet package readme; the authoritative reference
2. `README.md` (root) — Project Templates section
3. `docs/ai/CONTEXT.md` — section 12, Project Templates
4. `.claude/CLAUDE.md` — the `Flowly.Templates/` entry in Project Structure

The task is **not done** until all four are consistent.

## Template inventory

| Short name | Command | Purpose |
|---|---|---|
| `flowly` | `dotnet new flowly --transport <value> [options]` | Single Flowly ASP.NET Core project |
| `flowlyapp` | `dotnet new flowlyapp --transport <value> [options]` | Complete solution (Messages + Sender + Receiver) |
| `flowlyaspireapp` | `dotnet new flowlyaspireapp --transport <value> [options]` | Complete .NET Aspire solution |
| `flowlymessagelib` | `dotnet new flowlymessagelib [--jobs] [options]` | Message contracts class library |
| `flowlyskills` | `dotnet new flowlyskills` | Install Flowly Claude Code skills into `.claude/skills/` |

## `--transport` (required)

| Value | Alias | Transport |
|---|---|---|
| `rabbitmq` | `rmq` | RabbitMQ |
| `azureservicebus` | `asb` | Azure Service Bus |
| `inmemory` | `inm` | In-Memory (no broker) |

`flowlyapp` and `flowlyaspireapp` also accept `eventhubs` (alias `eh`): stream-only, implies `--stream`, and rejects `--call`, `--jobs` and `--deadletter` at scaffold time (through the conditionally required `eventhubs-streams-only-remove-call-jobs-deadletter` parameter). The `flowly` template does not offer it. Templates stay single-transport: never scaffold two transports in one project.

Transport is a single `--transport` parameter, **not** boolean flags: never document or generate `--rabbitmq` or `--AzureServiceBus`.

Optional feature flags: `--jobtracking`/`--jobs`, `--deadlettertracking`/`--deadletter`, `--opentelemetry`/`--otel`, `--inline`. Database flags (required with jobs or dead-letter tracking): `--sqlserver`, `--postgres`, `--sqlite`.

## SyncSkills MSBuild target

`SyncSkills` in `Flowly.Templates.csproj` copies `.claude/skills/**` into `content/flowly-skills/.claude/skills/` at build time. Those files are generated: never edit them; edit the source under `.claude/skills/`.

## Template parity: flowlyapp ↔ flowlyaspireapp

Both scaffold a dedicated `JobTracker` project (job state persistence) and a dedicated `DeadLetterTracker` project (monitors dead-letter sub-queues, persists failed messages) for `--jobs` / `--deadletter`; the Receiver is a pure message-processing worker. InMemory is the exception: everything is embedded in a single `App/` project in both. **When changing either template's architecture for these features, apply the equivalent change to the other.**

## File names are case-sensitive

Template content is scaffolded on Linux too, where `DeadLetterTracker.csproj` and `DeadletterTracker.csproj` are different files. Spell every project file and directory exactly as the solution files (`.slnx`) and `ProjectReference` items name them; macOS and Windows hide a mismatch. The CI step "Check template project paths" fails on any `.slnx` project path or `ProjectReference` in template content that does not resolve to an existing file.

## Adding or removing a template parameter

When a parameter in `content/flowly-project/.template.config/template.json` is added, removed or renamed: update `src/Flowly.Templates/README.md` first (source of truth), then `README.md`, `docs/ai/CONTEXT.md` and `.claude/CLAUDE.md`, then any skills in `.claude/skills/` that reference template usage.
