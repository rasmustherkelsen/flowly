using Azure.Storage.Blobs;
using Flowly;
using Flowly.AzureEventHubs;
using Flowly.MessageStreams;
using Messages;
using Receiver.Handlers;

var builder = Host.CreateApplicationBuilder(args);

var ownershipContainer = new BlobContainerClient(builder.Configuration.GetConnectionString("PartitionOwnership"), "sensor-reading-ownership");
await ownershipContainer.CreateIfNotExistsAsync();

builder.AddFlowly(flowly => flowly
    .UseAzureEventHubs("EventHubs", partitionOwnershipContainer: ownershipContainer)
    .AddSQLiteMessageStreamPersistence("ReadingCheckpoints")
    .AddMessageStreamHandler<SensorReadingMessage, SensorReadingHandler>()
    .UseMessageStreamCheckpoint<SensorReadingMessage>());

builder.Build().Run();
