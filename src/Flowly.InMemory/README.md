# Flowly.InMemory

In-memory transport provider for [Flowly](https://github.com/TriumfNas/SimpleTransit). All messaging operations run entirely in-process using .NET channels — no external broker is required.

## When to use

- **Testing** — fast, deterministic handler tests without a running broker
- **Local development** — get up and running instantly with no Docker or cloud setup
- **Lightweight scenarios** — single-process apps that don't need distributed messaging

## Installation

```
dotnet add package Flowly.InMemory
```

## Usage

```csharp
public class MyConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder) =>
        builder
            .UseInMemory()
            .AddMessageHandler<MyMessage, MyHandler>()
            .AddMessageSubmitter<MyMessage>();
}
```

Register in `Program.cs`:

```csharp
services.AddFlowly<MyConfig>(configuration);
```

## Options

```csharp
builder.UseInMemory(configure: opts =>
{
    opts.MaxMessageSizeBytes = 2 * 1024 * 1024; // 2 MB
    opts.ChannelCapacity = 500;                  // messages per queue before back-pressure
});
```

| Option | Default | Description |
|---|---|---|
| `MaxMessageSizeBytes` | 1 048 576 (1 MB) | Maximum serialized message size. Larger messages throw `MessageTooLargeException`. |
| `ChannelCapacity` | 1000 | Bounded channel capacity per queue. Writers block when full (back-pressure). |

## Supported features

| Feature | Supported |
|---|---|
| Regular message handlers (`MessageHandler<T>`) | ✓ |
| Batch message handlers (`BatchMessageHandler<T>`) | ✓ |
| Job handlers (`JobHandler<T>`) | ✓ |
| Recurring jobs (`RecurringJobHandler`) | ✓ |
| Event handlers (`EventHandlerBase<TEvent>`) | ✓ |
| Retry policy (`[RetryPolicy]`) | ✓ |
| Dead-letter tracking | ✓ |
| Scheduled delivery (retry delay) | ✓ (via `InMemoryScheduler` hosted service) |
| Topology creation | No-op (channels are created lazily) |
