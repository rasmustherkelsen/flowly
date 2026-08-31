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
