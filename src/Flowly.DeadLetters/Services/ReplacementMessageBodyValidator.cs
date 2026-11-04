using System.Text;
using System.Text.Json;

namespace Flowly.DeadLetters.Services;

internal static class ReplacementMessageBodyValidator
{
    internal const int MaxBytes = 256 * 1024;

    internal static void Validate(string storedMessageBody, string replacementMessageBody)
    {
        if (string.IsNullOrWhiteSpace(replacementMessageBody))
        {
            throw new InvalidReplacementMessageBodyException("The replacement message body must not be empty.");
        }

        if (Encoding.UTF8.GetByteCount(replacementMessageBody) > MaxBytes)
        {
            throw new InvalidReplacementMessageBodyException($"The replacement message body must not be larger than {MaxBytes} bytes (256 KiB) as UTF-8.");
        }

        if (IsJson(storedMessageBody) && !IsJson(replacementMessageBody))
        {
            throw new InvalidReplacementMessageBodyException("The stored message body is JSON, so the replacement message body must be valid JSON too.");
        }
    }

    private static bool IsJson(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
