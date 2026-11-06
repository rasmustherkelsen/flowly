namespace Flowly.AzureEventHubs.Tests.Emulator.Support;

internal sealed record HandlerSettings(string ConsumerGroup, StartPosition StartPosition, int MaxMessages = 1, TimeSpan? MaxWait = null);

internal sealed record HandledEvent<TMessage>(TMessage Body, int? Partition)
{
    public override string ToString() => $"{Body} @ partition {Partition?.ToString() ?? "-"}";
}

internal sealed record HandlerInvocation<TMessage>(IReadOnlyList<TMessage> Messages, int? Partition);

internal sealed class HandlerBehavior<TMessage>
{
    public Observed<HandledEvent<TMessage>> Handled { get; } = new();

    public Observed<HandlerInvocation<TMessage>> Invocations { get; } = new();

    public Func<IMessageStreamContext<TMessage>, Task>? Behavior { get; set; }
}

internal class RecordingHandler<TMessage> : MessageStreamHandler<TMessage>
{
    private static readonly AsyncLocal<HandlerSettings?> ConfigurationScope = new();

    private readonly HandlerBehavior<TMessage>? _behavior;

    public RecordingHandler()
    {
    }

    public RecordingHandler(HandlerBehavior<TMessage> behavior) => _behavior = behavior;

    public static void UseSettings(HandlerSettings settings) => ConfigurationScope.Value = settings;

    public override void Configure(MessageStreamHandlerOptions options)
    {
        var settings = ConfigurationScope.Value ?? throw new InvalidOperationException("Call RecordingHandler.UseSettings before registering the handler.");

        options.ConsumerName = settings.ConsumerGroup;
        options.StartPosition = settings.StartPosition;
        options.MaxMessagesBeforeProcessing = settings.MaxMessages;
        options.MaxWaitTime = settings.MaxWait ?? TimeSpan.FromMilliseconds(200);
    }

    public override async Task Handle(IMessageStreamContext<TMessage> messageContext)
    {
        var behavior = _behavior ?? throw new InvalidOperationException("The handler was created without its behavior.");

        behavior.Invocations.Add(new HandlerInvocation<TMessage>([.. messageContext.Messages], messageContext.Partition));

        if (behavior.Behavior is { } custom)
            await custom(messageContext);

        foreach (var message in messageContext.Messages)
        {
            behavior.Handled.Add(new HandledEvent<TMessage>(message, messageContext.Partition));
        }
    }
}

[RetryPolicy(2, 0)]
internal sealed class RetryingRecordingHandler<TMessage> : RecordingHandler<TMessage>
{
    public RetryingRecordingHandler()
    {
    }

    public RetryingRecordingHandler(HandlerBehavior<TMessage> behavior) : base(behavior)
    {
    }
}
