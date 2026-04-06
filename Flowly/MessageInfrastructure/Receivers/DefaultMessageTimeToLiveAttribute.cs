namespace Flowly.MessageInfrastructure.Receivers;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DefaultMessageTimeToLiveAttribute(string timeToLive) : Attribute
{
    public string TimeToLive { get; } = timeToLive;

    public TimeSpan GetValue()
    {
        if (TimeSpan.TryParse(TimeToLive, out var parsedTimeToLive))
        {
            return parsedTimeToLive;
        }

        throw new InvalidOperationException($"Could not parse {nameof(TimeToLive)} value '{TimeToLive}' as a TimeSpan.");
    }
}
