# Flowly.MessageStreams.Postgres

PostgreSQL backend for [Flowly.MessageStreams](https://rasmustherkelsen.github.io/flowly/) message stream checkpoint persistence.

```csharp
builder.UseRabbitMq("RabbitMQ");
builder.AddPostgresMessageStreamPersistence("MsgStreamDb");

builder.AddMessageStreamHandler<MyMessage, MyMessageHandler>();
builder.UseMessageStreamCheckpoint<MyMessage>();
```

By default (`enableMigrations: true`), a hosted service applies pending EF Core migrations at startup. To generate an idempotent SQL script instead:

```bash
dotnet ef migrations script --idempotent --project Flowly.MessageStreams.Postgres --output migrate.sql
```
