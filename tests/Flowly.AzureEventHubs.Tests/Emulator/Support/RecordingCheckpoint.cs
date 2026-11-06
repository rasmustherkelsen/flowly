namespace Flowly.AzureEventHubs.Tests.Emulator.Support;

internal sealed record SavedPosition(int? Partition, long Position, bool IsBootstrapSeed)
{
    public override string ToString() => $"partition {Partition?.ToString() ?? "-"} position {Position}{(IsBootstrapSeed ? " (seed)" : string.Empty)}";
}

internal sealed class RecordingCheckpoint<TMessage> : MessageStreamCheckpoint<TMessage>
{
    private const int NonPartitioned = -1;

    private readonly Dictionary<int, long> _positions = [];
    private readonly Lock _gate = new();

    public Observed<SavedPosition> Saves { get; } = new();

    public Observed<int?> PositionLookups { get; } = new();

    public Func<int?, Exception?>? FailLookup { get; set; }

    public void Store(int? partition, long position)
    {
        lock (_gate)
        {
            _positions[partition ?? NonPartitioned] = position;
        }
    }

    public long? Position(int? partition)
    {
        lock (_gate)
        {
            return _positions.TryGetValue(partition ?? NonPartitioned, out var position) ? position : null;
        }
    }

    protected internal override Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    protected internal override Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
    {
        PositionLookups.Add(context.Partition);

        if (FailLookup?.Invoke(context.Partition) is { } failure)
            return Task.FromException<long?>(failure);

        return Task.FromResult(Position(context.Partition));
    }

    protected internal override Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _positions[context.Partition ?? NonPartitioned] = context.Position;
        }

        Saves.Add(new SavedPosition(context.Partition, context.Position, context.IsBootstrapSeed));

        return Task.CompletedTask;
    }
}
