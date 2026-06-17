---
name: create-contracts-assembly
description: Create a shared message contracts assembly for a Flowly solution where multiple projects (senders and receivers) need to reference the same message types. Use when a solution has more than one deployable service exchanging Flowly messages.
---

Guide the user through creating a dedicated contracts project and wiring it into the solution. Read the solution file first to understand the existing project structure before proposing names or paths.

## When you need a contracts assembly

A shared contracts project is needed whenever **two or more deployable services exchange the same message**. The canonical case:

```
┌─────────────────┐         queue         ┌──────────────────────┐
│   Api (sender)  │ ──── OrderPlaced ───▶ │  Processor (handler) │
└─────────────────┘                        └──────────────────────┘
```

Both projects must reference the same `OrderPlacedMessage` type. Duplicating the record in each project leads to queue name drift, serialisation mismatches, and broken contracts when one side is updated.

**You do not need a contracts project if** a message is only ever handled and never sent (internal queue), or the solution has a single deployable project.

## Step 1 — Create the contracts project

Name it `<SolutionName>.Messages` or `<SolutionName>.Contracts`. The Flowly convention (seen in the reference samples) is `MessageContracts` for simple solutions. For larger solutions a name that reflects the bounded context works better (e.g. `Finance.Messages`).

```bash
dotnet new classlib -n <ContractsProjectName> -o src/<ContractsProjectName> --framework net10.0
```

Delete the auto-generated `Class1.cs`.

Minimal `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Flowly" Version="*" />
    <!-- Add Flowly.Jobs only if any message implements IJobMessage -->
  </ItemGroup>

</Project>
```

Add the project to the solution:

```bash
dotnet sln add src/<ContractsProjectName>/<ContractsProjectName>.csproj
```

## Step 2 — What belongs in the contracts project

**Include:**
- Message records (`public record OrderPlacedMessage(...)`)
- Event records (`public record OrderProcessedEvent(...)`)
- Job message records implementing `IJobMessage` (`public record ProcessOrder(...) : IJobMessage`)
- `[QueueName]` attributes on any message that needs a non-default queue name
- Enums and value objects used as message properties

**Do not include:**
- Handlers, services, or business logic
- EF Core entities or repositories
- Infrastructure dependencies (Azure SDK, database drivers)
- `FlowlyConfiguration` (the subclass) — that stays in each service project

The contracts project is a **pure data contract** layer. It should have minimal dependencies and change infrequently. Breaking changes here break all consumers.

## Step 3 — Define message types

Each message gets its own file. Keep the namespace flat — `<ContractsNamespace>` with no sub-folders unless the project grows large enough to warrant grouping by domain area.

**Regular message:**
```csharp
namespace <ContractsNamespace>;

public record OrderPlacedMessage(Guid OrderId, string CustomerEmail, decimal Amount);
```

**Message with explicit queue name:**
```csharp
namespace <ContractsNamespace>;

[QueueName("order-placed")]
public record OrderPlacedMessage(Guid OrderId, string CustomerEmail, decimal Amount);
```

Only add `[QueueName]` when the default convention (PascalCase → kebab-case, `Message` suffix stripped) produces the wrong name, or when the queue name must remain stable across class renames.

**Job message:**
```csharp
using Flowly.Jobs;

namespace <ContractsNamespace>;

public record ProcessOrder(Guid OrderId, string Description) : IJobMessage
{
    public string JobTypeName => "Process Order";
}
```

**Event (fan-out to multiple subscribers):**
```csharp
namespace <ContractsNamespace>;

public record OrderProcessedEvent(string OrderId, DateTimeOffset ProcessedAt);
```

## Step 4 — Reference from sender and receiver projects

Add a project reference to **every** project that sends or receives the messages:

```xml
<ItemGroup>
  <ProjectReference Include="..\<ContractsProjectName>\<ContractsProjectName>.csproj" />
</ItemGroup>
```

Or via CLI:
```bash
dotnet add src/Api reference src/<ContractsProjectName>
dotnet add src/Processor reference src/<ContractsProjectName>
```

After referencing, update `using` statements in `FlowlyConfiguration.cs` and handler files to import from the contracts namespace.

## Step 5 — Cross-solution / cross-repository scenario

If the sender and receiver live in different repositories, publish the contracts project as a NuGet package instead of using a project reference.

Add to the `.csproj`:
```xml
<PropertyGroup>
  <PackageId><ContractsProjectName></PackageId>
  <Version>1.0.0</Version>
  <Authors>Your Team</Authors>
  <Description>Flowly message contracts for <SolutionName></Description>
</PropertyGroup>
```

Publish:
```bash
dotnet pack src/<ContractsProjectName> -c Release -o ./nupkgs
dotnet nuget push ./nupkgs/*.nupkg --source <feed-url>
```

Consume in the other repository:
```xml
<PackageReference Include="<ContractsProjectName>" Version="*" />
```

**Versioning discipline:** treat the contracts package like a public API. A field added to a record is non-breaking. Removing or renaming a field, or changing a queue name, is breaking and requires a major version bump and coordinated deployment.

## Step 6 — Move existing message types

If message records currently live inside the sender or receiver project, move them to the contracts project now:

1. Copy the record files to the contracts project, update the namespace.
2. Add `<ProjectReference>` to the projects that previously owned them.
3. Delete the originals.
4. Fix any broken `using` statements.
5. Run `dotnet build` to confirm no references are broken.

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

- [ ] Contracts project created and added to solution
- [ ] `.csproj` references only `Flowly` (and `Flowly.Jobs` if needed)
- [ ] All shared message records are defined in the contracts project with `public` visibility
- [ ] `[QueueName]` applied where default name is wrong or must be stable
- [ ] Sender and receiver projects reference the contracts project
- [ ] No business logic, handlers, or infrastructure code in the contracts project
- [ ] If cross-repo: contracts project packaged and published as NuGet
- [ ] `dotnet build` passes with no errors
