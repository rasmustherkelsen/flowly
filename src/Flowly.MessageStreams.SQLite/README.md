# Flowly.MessageStreams.SQLite

SQLite backend for [Flowly.MessageStreams](https://rasmustherkelsen.github.io/flowly/) message stream checkpoint persistence.

```csharp
builder.UseRabbitMq("RabbitMQ");
builder.AddSQLiteMessageStreamPersistence("Data Source=flowly-message-streams.db");

builder.AddMessageStreamHandler<MyMessage, MyMessageHandler>();
builder.UseMessageStreamCheckpoint<MyMessage>();
```

By default (`enableMigrations: true`), a hosted service applies pending EF Core migrations at startup. To generate an idempotent SQL script instead:

```bash
dotnet ef migrations script --idempotent --project Flowly.MessageStreams.SQLite --output migrate.sql
```
