namespace Flowly.AzureEventHubs.Tests.Emulator;

public sealed class EmulatorFactAttribute : FactAttribute
{
    public EmulatorFactAttribute()
    {
        if (EmulatorEnvironment.ConnectionString is null)
            Skip = $"Set {EmulatorEnvironment.ConnectionVariable} to the Event Hubs emulator connection string to run this test.";
    }
}

public sealed class EmulatorStorageFactAttribute : FactAttribute
{
    public EmulatorStorageFactAttribute()
    {
        if (EmulatorEnvironment.ConnectionString is null || EmulatorEnvironment.StorageConnectionString is null)
            Skip = $"Set {EmulatorEnvironment.ConnectionVariable} and {EmulatorEnvironment.StorageConnectionVariable} to the Event Hubs emulator and Azurite connection strings to run this test.";
    }
}
