using System.Text.Json.Nodes;

namespace Flowly.AzureEventHubs.Tests.Emulator;

internal sealed record EmulatorHub(string Name, int PartitionCount, int ConsumerGroupCount)
{
    public int? ContractPartitions => PartitionCount > 1 ? PartitionCount : null;
}

internal static class EmulatorEntities
{
    public const string Single = "it-single";
    public const string Partitioned = "it-partitioned";
    public const string Ownership = "it-ownership";
    public const string Empty = "it-empty";
    public const string FirstEvent = "it-first-event";

    public const int SinglePartitions = 1;
    public const int PartitionedPartitions = 4;
    public const int OwnershipPartitions = 4;
    public const int EmptyPartitions = 2;

    private const string NamespaceName = "emulatorNs1";

    public static IReadOnlyList<EmulatorHub> Hubs { get; } =
    [
        new(Single, SinglePartitions, 20),
        new(Partitioned, PartitionedPartitions, 20),
        new(Ownership, OwnershipPartitions, 10),
        new(Empty, EmptyPartitions, 2),
        new(FirstEvent, 1, 2)
    ];

    private static readonly ConsumerGroupAllocator Allocator = new(Hubs);

    public static string NextConsumerGroup(string hubName) => Allocator.Next(hubName);

    public static string ConsumerGroupName(int number) => $"cg{number:00}";

    public static JsonNode ToConfiguration()
        => new JsonObject
        {
            ["UserConfig"] = new JsonObject
            {
                ["NamespaceConfig"] = new JsonArray(
                    new JsonObject
                    {
                        ["Type"] = "EventHub",
                        ["Name"] = NamespaceName,
                        ["Entities"] = new JsonArray([.. Hubs.Select(ToEntity)])
                    }),
                ["LoggingConfig"] = new JsonObject { ["Type"] = "Console" }
            }
        };

    private static JsonNode ToEntity(EmulatorHub hub)
        => new JsonObject
        {
            ["Name"] = hub.Name,
            ["PartitionCount"] = hub.PartitionCount.ToString(),
            ["ConsumerGroups"] = new JsonArray([.. Enumerable.Range(1, hub.ConsumerGroupCount).Select(number => (JsonNode)new JsonObject { ["Name"] = ConsumerGroupName(number) })])
        };
}

internal sealed class ConsumerGroupAllocator(IReadOnlyList<EmulatorHub> hubs)
{
    private readonly Dictionary<string, int> _allocated = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public string Next(string hubName)
    {
        var hub = hubs.Single(candidate => candidate.Name == hubName);

        lock (_gate)
        {
            var allocated = _allocated.GetValueOrDefault(hubName);
            _allocated[hubName] = allocated + 1;

            return EmulatorEntities.ConsumerGroupName(allocated % hub.ConsumerGroupCount + 1);
        }
    }
}
