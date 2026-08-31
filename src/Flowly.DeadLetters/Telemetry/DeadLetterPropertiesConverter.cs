using System.Diagnostics;
using System.Text.Json;

namespace Flowly.DeadLetters.Telemetry;

internal static class DeadLetterPropertiesConverter
{
    public static ActivityContext ParseActivityContext(string messagePropertiesJson)
    {
        var rawProperties = JsonSerializer.Deserialize<Dictionary<string, object>>(messagePropertiesJson) ?? [];

        return ParseActivityContext(ConvertProperties(rawProperties));
    }

    public static ActivityContext ParseActivityContext(Dictionary<string, object> applicationProperties)
    {
        if (!applicationProperties.TryGetValue("traceparent", out var raw) || raw is not string traceparent)
            return default;

        var tracestate = applicationProperties.TryGetValue("tracestate", out var tsRaw) && tsRaw is string ts ? ts : null;

        return ActivityContext.TryParse(traceparent, tracestate, isRemote: true, out var context) ? context : default;
    }

    public static Dictionary<string, object> ConvertProperties(Dictionary<string, object> rawProperties)
    {
        return rawProperties.ToDictionary(kvp => kvp.Key, kvp => ConvertJsonElement(kvp.Value));
    }

    private static object ConvertJsonElement(object value)
    {
        if (value is not JsonElement element)
            return value;

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element.ToString()
        };
    }
}
