# Entity Framework Core Conventions

## Read queries: no tracking, shallowest projection possible

Any query whose result is not going to be mutated and saved back via the same
`DbContext` must:

- Call `.AsNoTracking()` on the query root.
- Project with `.Select()` into only the columns/fields actually needed by the
  caller — a DTO, record, or anonymous type — instead of returning full tracked
  entities when only a handful of properties are used downstream.

```csharp
// Bad — tracks entities and pulls every column even though only two are used.
var jobs = await context.Jobs
    .Where(j => j.IsRecurringJob == false)
    .ToListAsync(cancellationToken);

return jobs.Select(j => new JobSummary(j.JobIdentifier, j.Status));
```

```csharp
// Good — no tracking, and only the needed columns are selected in SQL.
return await context.Jobs
    .AsNoTracking()
    .Where(j => j.IsRecurringJob == false)
    .Select(j => new JobSummary(j.JobIdentifier, j.Status))
    .ToListAsync(cancellationToken);
```

Only use a full, tracked entity fetch when the entity is going to be mutated
and persisted via `SaveChangesAsync` on that same context instance (e.g. a
repository's update/transition methods). Do not add `.AsNoTracking()` to those
— tracking is required for the update to be detected.

## Bulk updates and deletes: `ExecuteUpdateAsync`/`ExecuteDeleteAsync`, not fetch-then-save

Any update or delete that applies to a set of rows by a filter — not a single
already-loaded entity being transitioned through business logic — must use
`ExecuteUpdateAsync`/`ExecuteDeleteAsync` instead of loading tracked entities,
mutating them in memory, and calling `SaveChangesAsync`.

```csharp
// Bad — loads every matching row into memory and tracks it just to delete it.
var expired = await context.DeadLetters
    .Where(d => d.CreatedAt < cutoff)
    .ToListAsync(cancellationToken);

context.DeadLetters.RemoveRange(expired);
await context.SaveChangesAsync(cancellationToken);
```

```csharp
// Good — one DELETE statement, no tracking, no materialization.
await context.DeadLetters
    .Where(d => d.CreatedAt < cutoff)
    .ExecuteDeleteAsync(cancellationToken);
```

This applies equally to updates via `ExecuteUpdateAsync(x => x.SetProperty(...))`.
Reserve a load-mutate-`SaveChangesAsync` cycle for the case where the row is
already a single tracked entity being carried through a state transition (e.g.
a job's `Created → Started` step) — not for anything expressed as a `Where`
filter over a set of rows.

## Existence checks: `Any()`/`AnyAsync()`, not `Count()`

Never call `.Count()`/`.CountAsync()` (or `.Count() > 0`) purely to check
whether any row matches — it forces the database to scan/count every matching
row. Use `.Any()`/`.AnyAsync()`, which stops at the first match.

```csharp
// Bad — counts every matching row just to check it's non-zero.
if (await context.Jobs.CountAsync(j => j.JobType!.Name == jobTypeName) > 0) { ... }
```

```csharp
// Good.
if (await context.Jobs.AnyAsync(j => j.JobType!.Name == jobTypeName)) { ... }
```

## Rationale

- Every `DbContext` used for reads in Flowly (`Flowly.Jobs`, `Flowly.DeadLetters`,
  and any future EF-backed backend package) is created per-operation via
  `IDbContextFactory<T>.CreateDbContextAsync()` and disposed immediately after —
  there is no long-lived context to amortize tracking overhead against, so
  tracking on a read-only query is pure waste.
- This directly supports the low-allocation goal in
  `performance-conventions.md`: change tracking snapshots and full-entity
  materialization both allocate memory that a shallow, no-tracking projection
  avoids entirely.

## Where this applies

Any EF Core query in `Flowly.Jobs`, `Flowly.DeadLetters`, their `*.SqlServer`
/ `*.Postgres` / `*.SQLite` backend packages, `Flowly.Dashboard`, and any
future EF-backed package — including repository methods, count/aggregate
readers, and Dashboard-facing query services.
