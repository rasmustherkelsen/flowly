---
name: add-message-stream-persistence
description: Add built-in, database-backed position persistence to an existing Flowly MessageStreamHandler<T> using Flowly.MessageStreams (SQL Server, PostgreSQL, or SQLite) — RabbitMQ only. Use when the user wants restart-survival for a stream handler backed by a real database, with no hand-written storage code. For custom storage (a cache, a file, a bespoke repository), use the add-message-stream-checkpoint skill instead.
arguments:
  - name: messageName
    description: "PascalCase message class name of the existing stream, including the Message suffix. Example: TelemetryReadingMessage"
    required: true
---

Add built-in checkpoint persistence for an **existing** `MessageStreamHandler<$0>` registration, so the stream's read position survives a process restart using one of Flowly's own SQL Server/PostgreSQL/SQLite backends — no hand-written storage code. Follow all steps below.

> **Important constraints to communicate upfront:**
> - **RabbitMQ only.** Registering checkpoint persistence against an InMemory-backed stream throws `InvalidOperationException` — the in-process log has no cross-restart persistence, so a saved position would point at data that no longer exists after a restart.
> - **The first run's position is stored up front.** With a registered checkpoint, `StartPosition.Last()` and `StartPosition.Timestamp(...)` are resolved to an offset and saved before the handler starts consuming, so a restart before the first message does not skip messages published in the meantime; the table can therefore show a position of `-1` (start at offset 0) for a reader that has processed nothing. If the broker cannot resolve it in time, a warning is logged and the position is stored after the first processed batch instead. `StartPosition.First()` and `.Offset(n)` are not stored up front.
> - **Multiple named stores are supported.** The first unnamed `Add<Provider>MessageStreamPersistence(...)` call becomes the default; a second unnamed registration throws — pass `name: "..."` to register additional stores, and the matching name to `UseMessageStreamCheckpoint<T>("...")`.
> - **Migrations run automatically by default.** `enableMigrations: true` (the default) applies pending EF Core migrations for the checkpoint schema at startup. Set `enableMigrations: false` for teams that apply schema changes during deployment instead — see the "Idempotent migration scripts" note below.

## Step 1 — Find the existing stream handler

```bash
grep -rn "MessageStreamHandler<$0>" --include="*.cs" .
```

- **No match** → stop and tell the user: "No `MessageStreamHandler<$0>` was found. Checkpoint persistence attaches to an existing stream handler — use `/create-message-stream-handler` first if `$0` doesn't have one yet." Do not scaffold anything with nothing to attach it to.
- **Match found** → note the handler's project and namespace; persistence is registered in the **same project** as the handler (the consumer side), not the producer.

## Step 2 — Confirm the transport is RabbitMQ

```bash
grep -r "UseRabbitMq\|UseInMemory\|UseAzureServiceBus" --include="*.cs" <handler project path>
```

- **RabbitMQ found** → proceed.
- **InMemory found** → stop and tell the user: "This stream is InMemory-backed. Message stream checkpoint persistence is RabbitMQ-only — the in-process log has no cross-restart persistence for a saved position to point at." Do not scaffold anything.
- **Azure Service Bus found, or no transport found** → this shouldn't be possible for a project that already has a `MessageStreamHandler` (streams require RabbitMQ or InMemory), but if it happens, stop and flag it to the user as unexpected before proceeding.

## Step 3 — Ask which storage backend

Ask the user:

> "Which database should back this stream's checkpoint persistence?
> - **SQL Server** (`Flowly.MessageStreams.SqlServer`)
> - **PostgreSQL** (`Flowly.MessageStreams.Postgres`)
> - **SQLite file** (`Flowly.MessageStreams.SQLite`)
>
> Which would you like?"

Wait for the user's answer before writing any code.

## Step 4 — Add the NuGet package

Add the chosen backend package to the handler's project `.csproj`:

| Choice | Package |
|---|---|
| SQL Server | `Flowly.MessageStreams.SqlServer` |
| PostgreSQL | `Flowly.MessageStreams.Postgres` |
| SQLite | `Flowly.MessageStreams.SQLite` |

```xml
<PackageReference Include="Flowly.MessageStreams.SqlServer" Version="*" />
```

## Step 5 — Register in FlowlyConfiguration

Find the project's `FlowlyConfiguration` subclass. Add the persistence registration **before** `AddMessageStreamHandler<$0, ...>()` — order doesn't strictly matter (the InMemory guard is checked symmetrically either way), but this ordering matches Flowly's own documentation and samples — then `UseMessageStreamCheckpoint<$0>()` after:

| Choice | Registration |
|---|---|
| SQL Server | `.AddSqlServerMessageStreamPersistence("MsgStreamDb")` |
| PostgreSQL | `.AddPostgresMessageStreamPersistence("MsgStreamDb")` |
| SQLite file | `.AddSQLiteMessageStreamPersistence("MsgStreamDb")` |

Example result:

```csharp
using Flowly.MessageStreams;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseRabbitMq(connection: "RabbitMQ");

        builder.AddSqlServerMessageStreamPersistence("MsgStreamDb");
        builder.AddMessageStreamHandler<$0, $0Handler>();
        builder.UseMessageStreamCheckpoint<$0>();
    }
}
```

If the user needs more than one named persistence store in the same project (e.g. different databases for different streams), pass `name:` to the registration call and the matching name to `UseMessageStreamCheckpoint<$0>("...")` — the first unnamed registration is always the default.

## Step 6 — Add connection string

Add the connection string to `appsettings.Development.json` (and `appsettings.json` as a placeholder without secrets):

**SQL Server:**
```json
{
  "ConnectionStrings": {
    "MsgStreamDb": "Server=localhost,1433;Database=FlowlyMessageStreams;User Id=sa;Password=Password1!;TrustServerCertificate=True"
  }
}
```

**PostgreSQL:**
```json
{
  "ConnectionStrings": {
    "MsgStreamDb": "Host=localhost;Database=FlowlyMessageStreams;Username=postgres;Password=postgres"
  }
}
```

**SQLite file:**
```json
{
  "ConnectionStrings": {
    "MsgStreamDb": "Data Source=flowly-message-streams.db"
  }
}
```

## Step 7 — Mention idempotent migration scripts, if relevant

If the user mentions applying schema changes during deployment rather than at app startup, tell them to set `enableMigrations: false` on the registration call and generate a SQL script instead:

```bash
dotnet ef migrations script --idempotent --project <the chosen provider project, e.g. Flowly.MessageStreams.SqlServer> --output migrate.sql
```

This only applies to Flowly's own NuGet packages if the user wants to inspect Flowly's shipped migrations directly — for a normal consumer project, this isn't something they run against their own project, since the migrations live inside the `Flowly.MessageStreams.*` package itself. If asked how to apply Flowly's checkpoint schema without `enableMigrations: true`, explain that the migrations are embedded in the installed `Flowly.MessageStreams.<Provider>` package and can be scripted via `dotnet ef migrations script --idempotent` run against that package's project if they have the Flowly source, or otherwise recommend leaving `enableMigrations: true` (the default) since it is the supported path for consumers.

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

- [ ] Confirmed `MessageStreamHandler<$0>` already exists (did not scaffold persistence with nothing to attach it to)
- [ ] Confirmed the target project uses RabbitMQ, not InMemory
- [ ] Asked the user which backend (SQL Server, PostgreSQL, or SQLite), without guessing
- [ ] Package reference added to the handler's project
- [ ] `FlowlyConfiguration` updated with `Add<Provider>MessageStreamPersistence(...)` and `UseMessageStreamCheckpoint<$0>()`
- [ ] Connection string added to `appsettings.Development.json`
- [ ] `dotnet build` passes with no errors
