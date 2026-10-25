using System.Globalization;

namespace Flowly.AzureEventHubs;

internal static class EventHubsPartitionIds
{
    public static IReadOnlyList<string> Expected(int? declaredPartitionCount)
        => [.. Enumerable.Range(0, declaredPartitionCount ?? 1).Select(ToPartitionId)];

    public static bool Matches(IReadOnlyCollection<string> actualPartitionIds, int? declaredPartitionCount)
    {
        var expected = Expected(declaredPartitionCount);

        return actualPartitionIds.Count == expected.Count && expected.ToHashSet(StringComparer.Ordinal).SetEquals(actualPartitionIds);
    }

    public static string ToPartitionId(int partition) => partition.ToString(CultureInfo.InvariantCulture);

    public static int? ToPartition(string partitionId)
        => int.TryParse(partitionId, NumberStyles.None, CultureInfo.InvariantCulture, out var partition) && ToPartitionId(partition) == partitionId
            ? partition
            : null;
}
