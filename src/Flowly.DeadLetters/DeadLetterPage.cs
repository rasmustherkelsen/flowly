namespace Flowly.DeadLetters;

/// <summary>
///     The result of <see cref="IDeadLetterService.QueryDeadLetters"/>: one page of dead letters and, when requested, the number of matches.
/// </summary>
/// <param name="Items">
///     The dead letters of the requested page, newest first by <see cref="IDeadLetter.DeadLetteredAt"/> with the
///     <see cref="IDeadLetter.MessageId"/> as tie-breaker. Empty when nothing matches or <see cref="DeadLetterQuery.Skip"/> is beyond the last match.
/// </param>
/// <param name="TotalCount">
///     The number of dead letters matching the filters of the query regardless of paging, or <see langword="null"/>
///     when <see cref="DeadLetterQuery.IncludeTotalCount"/> was not set.
/// </param>
public sealed record DeadLetterPage(IReadOnlyList<IDeadLetter> Items, int? TotalCount);
