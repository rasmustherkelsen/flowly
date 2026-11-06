using Azure.Storage.Blobs;

namespace Flowly.AzureEventHubs.Tests.Fakes;

internal static class UnconnectedBlobContainerClient
{
    public static BlobContainerClient Create() => new(new Uri("https://example.blob.core.windows.net/ownership"));
}
