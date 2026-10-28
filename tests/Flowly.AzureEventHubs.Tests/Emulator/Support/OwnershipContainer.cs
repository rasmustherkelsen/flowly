using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Flowly.AzureEventHubs.Tests.Emulator.Support;

internal sealed class OwnershipContainer : IAsyncDisposable
{
    private OwnershipContainer(BlobContainerClient client) => Client = client;

    public BlobContainerClient Client { get; }

    public static async Task<OwnershipContainer> Create()
    {
        var client = new BlobContainerClient(EmulatorEnvironment.RequiredStorageConnectionString, $"flowly-it-{Guid.NewGuid():N}");

        await client.CreateAsync();

        return new OwnershipContainer(client);
    }

    public async Task<IReadOnlyDictionary<int, string>> Owners(string hubName, string consumerGroup)
    {
        var owners = new Dictionary<int, string>();
        var prefix = $"/{hubName}/{consumerGroup}/ownership/";

        await foreach (var blob in Client.GetBlobsAsync(BlobTraits.Metadata))
        {
            if (blob.Name.Contains(prefix, StringComparison.Ordinal) && int.TryParse(blob.Name[(blob.Name.LastIndexOf('/') + 1)..], out var partition))
                owners[partition] = blob.Metadata.TryGetValue("ownerid", out var owner) ? owner : string.Empty;
        }

        return owners;
    }

    public async Task<IReadOnlyDictionary<int, DateTimeOffset>> LastRenewals(string hubName, string consumerGroup)
    {
        var renewals = new Dictionary<int, DateTimeOffset>();
        var prefix = $"/{hubName}/{consumerGroup}/ownership/";

        await foreach (var blob in Client.GetBlobsAsync())
        {
            if (blob.Name.Contains(prefix, StringComparison.Ordinal) && int.TryParse(blob.Name[(blob.Name.LastIndexOf('/') + 1)..], out var partition) && blob.Properties.LastModified is { } lastModified)
                renewals[partition] = lastModified;
        }

        return renewals;
    }

    public async ValueTask DisposeAsync() => await Client.DeleteIfExistsAsync();
}
