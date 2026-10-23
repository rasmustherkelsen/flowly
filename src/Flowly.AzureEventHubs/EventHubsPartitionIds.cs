using System.Globalization;

namespace Flowly.AzureEventHubs;

internal static class EventHubsPartitionIds
{
    public static IReadOnlyList<string> Expected(int? declaredPartitionCount)
        => [.. Enumerable.Range(0, declaredPartitionCount ?? 1).Select(partition => partition.ToString(CultureInfo.InvariantCulture))];

    public static bool Matches(IReadOnlyCollection<string> actualPartitionIds, int? declaredPartitionCount)
    {
        var expected = Expected(declaredPartitionCount);

        return actualPartitionIds.Count == expected.Count && expected.ToHashSet(StringComparer.Ordinal).SetEquals(actualPartitionIds);
    }
}
