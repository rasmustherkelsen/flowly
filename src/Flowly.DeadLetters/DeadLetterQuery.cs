namespace Flowly.DeadLetters;

/// <summary>
///     Describes which dead letters <see cref="IDeadLetterService.QueryDeadLetters"/> returns: optional filters, a page (<see cref="DefaultTake"/> dead letters unless <see cref="Take"/> is set)
///     and whether the total number of matching dead letters is wanted as well.
///     Results are always ordered newest first by <see cref="IDeadLetter.DeadLetteredAt"/>, with the
///     <see cref="IDeadLetter.MessageId"/> as tie-breaker, so a page is stable between calls while the data does not change.
/// </summary>
/// <remarks>
///     All filters are exact matches and are combined with AND. A filter left at <see langword="null"/> does not restrict the result.
///     Whether the text filters and the message id tie-breaker are case sensitive follows the collation of the underlying database.
/// </remarks>
public sealed record DeadLetterQuery
{
    /// <summary>
    ///     The page size applied when <see cref="Take"/> is <see langword="null"/>: <c>100</c>.
    /// </summary>
    public const int DefaultTake = 100;

    private readonly int _skip;
    private readonly int? _take;

    /// <summary>
    ///     Only return dead letters from this queue. For an event subscription dead letter this is the topic name.
    ///     <see langword="null"/> (the default) does not filter on the queue.
    /// </summary>
    public string? QueueName { get; init; }

    /// <summary>
    ///     Only return dead letters from this event subscription; a queue dead letter has no subscription and never matches.
    ///     <see langword="null"/> (the default) does not filter on the subscription.
    /// </summary>
    public string? SubscriptionName { get; init; }

    /// <summary>
    ///     Only return dead letters with this status. <see langword="null"/> (the default) does not filter on the status.
    /// </summary>
    public DeadLetterStatus? Status { get; init; }

    /// <summary>
    ///     Only return the dead letter with this message id. <see langword="null"/> (the default) does not filter on the message id.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    ///     The number of matching dead letters to skip before the page starts, counted in the newest-first order.
    ///     Defaults to <c>0</c>. A value beyond the number of matches yields an empty page.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int Skip
    {
        get => _skip;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _skip = value;
        }
    }

    /// <summary>
    ///     The maximum number of dead letters in the page. <see langword="null"/> (the default) returns at most
    ///     <see cref="DefaultTake"/> (<c>100</c>) dead letters after <see cref="Skip"/>; <c>0</c> returns no items, which together with
    ///     <see cref="IncludeTotalCount"/> is a count-only query. There is no upper bound here: page through a larger result with
    ///     <see cref="Skip"/>, or use <see cref="IDeadLetterService.GetDeadLetters(System.Threading.CancellationToken)"/> to load everything.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int? Take
    {
        get => _take;
        init
        {
            if (value is { } take)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(take);
            }

            _take = value;
        }
    }

    internal int EffectiveTake => _take ?? DefaultTake;

    /// <summary>
    ///     When <see langword="true"/>, <see cref="DeadLetterPage.TotalCount"/> holds the number of dead letters matching the filters
    ///     regardless of <see cref="Skip"/> and <see cref="Take"/>, which costs one extra count query. Defaults to <see langword="false"/>.
    /// </summary>
    public bool IncludeTotalCount { get; init; }
}
