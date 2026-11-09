namespace Flowly.DeadLetters.Services;

internal static class DeadLetterQueryEvaluator
{
    internal static DeadLetterPage Evaluate(IEnumerable<IDeadLetter> deadLetters, DeadLetterQuery deadLetterQuery)
    {
        var matching = deadLetters
            .Where(deadLetter => deadLetterQuery.QueueName is null || deadLetter.QueueName == deadLetterQuery.QueueName)
            .Where(deadLetter => deadLetterQuery.SubscriptionName is null || deadLetter.SubscriptionName == deadLetterQuery.SubscriptionName)
            .Where(deadLetter => deadLetterQuery.Status is null || deadLetter.Status == deadLetterQuery.Status)
            .Where(deadLetter => deadLetterQuery.MessageId is null || deadLetter.MessageId == deadLetterQuery.MessageId)
            .ToList();

        var items = matching
            .OrderByDescending(deadLetter => deadLetter.DeadLetteredAt)
            .ThenBy(deadLetter => deadLetter.MessageId, StringComparer.Ordinal)
            .Skip(deadLetterQuery.Skip)
            .Take(deadLetterQuery.EffectiveTake)
            .ToList();

        return new DeadLetterPage(items, deadLetterQuery.IncludeTotalCount ? matching.Count : null);
    }
}
