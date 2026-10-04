using System.Diagnostics;
using Flowly.MessageInfrastructure.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.BackgroundServices;

/// <summary>
///     Resolves and persists <see cref="MessageStreamCheckpoint{TMessage}" /> positions. Shared between
///     <see cref="MessageStreamProcessingBackgroundService{TMessage,THandler}" /> (partition always <see langword="null" />)
///     and <see cref="PartitionedMessageStreamProcessingBackgroundService{TMessage,THandler}" /> (one call per
///     partition), so both go through identical checkpoint semantics. Instrumenting here — rather than inside a
///     specific <see cref="MessageStreamCheckpoint{TMessage}" /> implementation — gives OpenTelemetry visibility to
///     every checkpoint, built-in or hand-rolled, for free.
/// </summary>
internal static class MessageStreamCheckpointHelper
{
    private const string InMemoryMessagingSystem = "in-memory";

    public static void ThrowIfCheckpointRegisteredForInMemory<TMessage>(IServiceScopeFactory serviceScopeFactory, string messagingSystem)
    {
        if (messagingSystem != InMemoryMessagingSystem) return;

        using var scope = serviceScopeFactory.CreateScope();
        if (scope.ServiceProvider.GetService<IServiceProviderIsService>()?.IsService(typeof(MessageStreamCheckpoint<TMessage>)) != true) return;

        throw new InvalidOperationException(
            $"A {nameof(MessageStreamCheckpoint<TMessage>)}<{typeof(TMessage).Name}> is registered, but InMemory-backed streams do not " +
            "support checkpointing — the underlying log has no cross-restart persistence of its own, so a persisted position would " +
            "point at data that no longer exists after a restart.");
    }

    public static async Task<StartPosition> ResolveStartPosition<TMessage>(
        IServiceScopeFactory serviceScopeFactory,
        string consumerName,
        int? partition,
        StartPosition fallback,
        string handlerName,
        string queueName,
        IHandlerInstrumentation handlerInstrumentation,
        CancellationToken cancellationToken,
        bool initializeCheckpoint = true)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var checkpoint = scope.ServiceProvider.GetService<MessageStreamCheckpoint<TMessage>>();
        if (checkpoint is null) return fallback;

        var context = new MessageStreamCheckpointContext(consumerName, partition);

        if (initializeCheckpoint)
            await checkpoint.InitializeCheckpoint(context, cancellationToken);

        var storedPosition = await checkpoint.GetStreamPosition(context, cancellationToken);

        handlerInstrumentation.RecordCheckpointResolved(handlerName, queueName, storedPosition.HasValue);

        return storedPosition.HasValue ? StartPosition.Offset(storedPosition.Value + 1) : fallback;
    }

    public static async Task SaveStreamPositionIfPresent<TMessage>(
        IServiceScopeFactory serviceScopeFactory,
        string consumerName,
        int? partition,
        long position,
        string handlerName,
        string queueName,
        IHandlerInstrumentation handlerInstrumentation,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var checkpoint = scope.ServiceProvider.GetService<MessageStreamCheckpoint<TMessage>>();
        if (checkpoint is null) return;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await checkpoint.SaveStreamPosition(new MessageStreamCheckpointSaveContext(consumerName, partition, position), cancellationToken);
            handlerInstrumentation.RecordCheckpointSaved(handlerName, queueName, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch
        {
            handlerInstrumentation.RecordCheckpointSaveFailed(handlerName, queueName);
            throw;
        }
    }
}
