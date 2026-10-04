# Flowly.MessageStreams.SqlServer

SQL Server backend for [Flowly.MessageStreams](https://rasmustherkelsen.github.io/flowly/) message stream checkpoint persistence.

```csharp
builder.UseRabbitMq(connection: "RabbitMQ");
builder.AddSqlServerMessageStreamPersistence("MsgStreamDb");

builder.AddMessageStreamHandler<MyMessage, MyMessageHandler>();
builder.UseMessageStreamCheckpoint<MyMessage>();
```

By default (`enableMigrations: true`), a hosted service applies pending EF Core migrations at startup. To generate an idempotent SQL script instead:

```bash
dotnet ef migrations script --idempotent --project Flowly.MessageStreams.SqlServer --output migrate.sql
```
