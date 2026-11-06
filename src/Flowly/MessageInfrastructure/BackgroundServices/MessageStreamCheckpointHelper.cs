using System.Diagnostics;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        ILogger logger,
        CancellationToken cancellationToken,
        bool initializeCheckpoint = true,
        IStreamStartOffsetResolver? startOffsetResolver = null,
        Action? checkpointInitialized = null)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var checkpoint = scope.ServiceProvider.GetService<MessageStreamCheckpoint<TMessage>>();
        if (checkpoint is null) return fallback;

        var context = new MessageStreamCheckpointContext(consumerName, partition);

        if (initializeCheckpoint)
        {
            await checkpoint.InitializeCheckpoint(context, cancellationToken);
            checkpointInitialized?.Invoke();
        }

        var storedPosition = await checkpoint.GetStreamPosition(context, cancellationToken);

        if (storedPosition.HasValue)
        {
            handlerInstrumentation.RecordCheckpointResolved(handlerName, queueName, CheckpointResolution.Resumed);
            return StartPosition.Offset(storedPosition.Value + 1);
        }

        if (startOffsetResolver is null || !MovesWhileProcessIsDown(fallback))
        {
            handlerInstrumentation.RecordCheckpointResolved(handlerName, queueName, CheckpointResolution.Fresh);
            return fallback;
        }

        return await SeedStartPosition(checkpoint, startOffsetResolver, context, fallback, handlerName, queueName, handlerInstrumentation, logger, cancellationToken);
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

    private static async Task<StartPosition> SeedStartPosition<TMessage>(
        MessageStreamCheckpoint<TMessage> checkpoint,
        IStreamStartOffsetResolver startOffsetResolver,
        MessageStreamCheckpointContext context,
        StartPosition fallback,
        string handlerName,
        string queueName,
        IHandlerInstrumentation handlerInstrumentation,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var startOffset = await startOffsetResolver.ResolveStartOffset(queueName, context.Partition, fallback, cancellationToken);

            if (startOffset is null)
            {
                logger.LogWarning(
                    "{HandlerName} could not resolve the start position of stream queue '{QueueName}' partition {Partition} to an offset. " +
                    "The configured start position is used and the position is stored after the first processed batch instead.",
                    handlerName, queueName, context.Partition);
                handlerInstrumentation.RecordCheckpointResolved(handlerName, queueName, CheckpointResolution.SeedFailed);
                return fallback;
            }

            await checkpoint.SaveStreamPosition(
                new MessageStreamCheckpointSaveContext(context.ConsumerName, context.Partition, startOffset.Value - 1) { IsBootstrapSeed = true },
                cancellationToken);

            handlerInstrumentation.RecordCheckpointResolved(handlerName, queueName, CheckpointResolution.Seeded);
            return StartPosition.Offset(startOffset.Value);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e,
                "{HandlerName} could not fix the start position of stream queue '{QueueName}' partition {Partition} in the checkpoint store. " +
                "The configured start position is used and the position is stored after the first processed batch instead.",
                handlerName, queueName, context.Partition);
            handlerInstrumentation.RecordCheckpointResolved(handlerName, queueName, CheckpointResolution.SeedFailed);
            return fallback;
        }
    }

    private static bool MovesWhileProcessIsDown(StartPosition startPosition)
        => startPosition.Match(first: () => false, last: () => true, offset: _ => false, timestamp: _ => true);
}
