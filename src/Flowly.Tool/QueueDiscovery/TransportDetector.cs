namespace Flowly.Tool.QueueDiscovery;

internal static class TransportDetector
{
    public static IReadOnlySet<string> Detect(IReadOnlyList<QueueDiscoverySource> sources)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            var dir = Path.GetDirectoryName(source.Assembly.FullName)!;

            if (File.Exists(Path.Combine(dir, "Flowly.AzureServiceBus.dll")))
            {
                result.Add("AzureServiceBus");
            }

            if (File.Exists(Path.Combine(dir, "Flowly.RabbitMQ.dll")))
            {
                result.Add("RabbitMQ");
            }
        }

        return result;
    }
}
