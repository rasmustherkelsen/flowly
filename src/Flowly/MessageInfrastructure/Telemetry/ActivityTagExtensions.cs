using System.Diagnostics;

namespace Flowly.MessageInfrastructure.Telemetry;

internal static class ActivityTagExtensions
{
    internal static void ApplyTagsFrom(this Activity? activity, object? message)
    {
        if (activity == null || message is not IOpenTelemetryTagsProvider provider)
            return;

        foreach (var tag in provider.GetOpenTelemetryTags())
            activity.SetTag(tag.Key, tag.Value);
    }
}
