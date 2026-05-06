namespace Flowly.Tool.QueueDiscovery;

internal sealed class MissingTransportProviderException(string message) : InvalidOperationException(message);
