# NuGet Package Description Conventions

## Keeping descriptions current

NuGet `<Description>` tags in `.csproj` files must be updated in the same change as the feature they describe.

## Database backend enumeration

Parent packages that require a database backend (`Flowly.Jobs`, `Flowly.DeadLetters`) must explicitly list **all three** supported backends in their description:

- `SqlServer`
- `Postgres`
- `SQLite`

When a new database backend package is added, update the parent package description to include it. When one is removed, remove it from the description.

**Example — correct:**
```xml
<Description>Job state tracking and CRON scheduling for Flowly. Requires a database backend: Flowly.Jobs.SqlServer, Flowly.Jobs.Postgres, or Flowly.Jobs.SQLite.</Description>
```

**Example — incorrect (too vague, omits options):**
```xml
<Description>Job state tracking and CRON scheduling for Flowly. Job state tracking requires an underlying database so take your dependency on Flowly.Jobs.X where X is the database you want to use.</Description>
```
