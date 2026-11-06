namespace Flowly.AzureEventHubs;

internal sealed class EventHubsOwnershipLostException(string message) : Exception(message);
