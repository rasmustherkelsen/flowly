using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.BackgroundServices;

/// <summary>
///     Resolves and persists <see cref="MessageStreamCheckpoint{TMessage}" /> positions. Shared between
///     <see cref="MessageStreamProcessingBackgroundService{TMessage,THandler}" /> (partition always <see langword="null" />)
///     and <see cref="PartitionedMessageStreamProcessingBackgroundService{TMessage,THandler}" /> (one call per
///     partition), so both go through identical checkpoint semantics.
/// </summary>
internal static class MessageStreamCheckpointHelper
{
    public static async Task<StartPosition> ResolveStartPosition<TMessage>(
        IServiceScopeFactory serviceScopeFactory,
        string consumerName,
        int? partition,
        StartPosition fallback,
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

        return storedPosition.HasValue ? StartPosition.Offset(storedPosition.Value + 1) : fallback;
    }

    public static async Task SaveStreamPositionIfPresent<TMessage>(
        IServiceScopeFactory serviceScopeFactory,
        string consumerName,
        int? partition,
        long position,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var checkpoint = scope.ServiceProvider.GetService<MessageStreamCheckpoint<TMessage>>();
        if (checkpoint is null) return;

        await checkpoint.SaveStreamPosition(new MessageStreamCheckpointSaveContext(consumerName, partition, position), cancellationToken);
    }
}
