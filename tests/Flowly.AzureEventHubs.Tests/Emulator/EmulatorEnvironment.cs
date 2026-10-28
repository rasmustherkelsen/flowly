namespace Flowly.AzureEventHubs.Tests.Emulator;

internal static class EmulatorEnvironment
{
    public const string ConnectionVariable = "FLOWLY_EVENTHUBS_EMULATOR_CONNECTION";
    public const string StorageConnectionVariable = "FLOWLY_EVENTHUBS_EMULATOR_STORAGE_CONNECTION";

    public static string? ConnectionString => Read(ConnectionVariable);

    public static string? StorageConnectionString => Read(StorageConnectionVariable);

    public static string RequiredConnectionString
        => ConnectionString ?? throw new InvalidOperationException($"The environment variable {ConnectionVariable} is not set.");

    public static string RequiredStorageConnectionString
        => StorageConnectionString ?? throw new InvalidOperationException($"The environment variable {StorageConnectionVariable} is not set.");

    private static string? Read(string variable) => Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? value : null;
}
