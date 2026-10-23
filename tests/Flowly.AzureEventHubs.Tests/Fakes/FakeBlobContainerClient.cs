using Azure;
using Azure.Storage.Blobs;

namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class FakeBlobContainerClient(bool exists) : BlobContainerClient
{
    public override string Name => "ownership";

    public override Task<Response<bool>> ExistsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Response.FromValue(exists, null!));
}
