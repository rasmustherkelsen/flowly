namespace Flowly.AzureEventHubs.Tests.Emulator.Support;

internal static class Probe
{
    public const string Tag = "probe";

    private static readonly TimeSpan Attempt = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Overall = TimeSpan.FromSeconds(45);

    public static async Task UntilDelivered<TMessage>(
        EmulatorTransport transport,
        string hubName,
        Observed<ReceivedEvent<TMessage>> received,
        int partitions = 1)
        where TMessage : class, ITaggedEvent<TMessage>
    {
        var started = TimeProvider.System.GetTimestamp();
        var probeNumber = 0;

        while (true)
        {
            await transport.Record(hubName, TMessage.Create(Tag, probeNumber++));

            try
            {
                await received.WaitUntil(events => events.Select(@event => @event.Partition ?? 0).Distinct().Count() >= partitions, "the reader to attach to every partition", Attempt);

                return;
            }
            catch (TimeoutException) when (TimeProvider.System.GetElapsedTime(started) < Overall)
            {
            }
        }
    }

    public static async Task UntilHandled<TMessage>(EmulatorHost<TMessage> host, int partitions = 1)
        where TMessage : class, ITaggedEvent<TMessage>
    {
        var started = TimeProvider.System.GetTimestamp();
        var probeNumber = 0;

        while (true)
        {
            await host.Record(TMessage.Create(Tag, probeNumber++));

            try
            {
                await host.Behavior.Handled.WaitUntil(events => events.Select(@event => @event.Partition ?? 0).Distinct().Count() >= partitions, "the handler to attach to every partition", Attempt);

                return;
            }
            catch (TimeoutException) when (TimeProvider.System.GetElapsedTime(started) < Overall)
            {
            }
        }
    }
}
