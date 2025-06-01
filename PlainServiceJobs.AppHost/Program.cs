var builder = DistributedApplication.CreateBuilder(args);

var azureServiceBus = builder
    .AddAzureServiceBus("EmulatorNamespace")
    .RunAsEmulator(c => c.WithLifetime(ContainerLifetime.Persistent));

var rebuildIndexQueue = azureServiceBus.AddServiceBusQueue("rebuild-index");
rebuildIndexQueue.WithProperties(q =>
{
    q.LockDuration = TimeSpan.FromMinutes(5);
}); 


var performStitchingQueue = azureServiceBus.AddServiceBusQueue("perform-stitching");

// ** SYSTEM QUEUES **

var createJobStateQueue = azureServiceBus.AddServiceBusQueue("create-job-state");
var createRecurringJobStateQueue = azureServiceBus.AddServiceBusQueue("create-recurring-job-state");
var updateJobStateQueue = azureServiceBus.AddServiceBusQueue("update-job-state");
var jobFailedQueue = azureServiceBus.AddServiceBusQueue("job-failed");
var updateJobCustomStateQueue = azureServiceBus.AddServiceBusQueue("update-job-custom-state");
var startRecurringJobQueue = azureServiceBus.AddServiceBusQueue("start-recurring-job");
var recurringJobsQueue = azureServiceBus.AddServiceBusQueue("recurring-jobs");
recurringJobsQueue.WithProperties(q => q.RequiresSession = true);

// *******************

var sqlServer = builder
    .AddSqlServer("SqlServer", builder.AddParameter("sql-password", secret: true, value: "6v9p}-3Y(eWz7Cqwy6-93Y"), port: 6508)
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.BackendProcessor>("BackendProcessor")
    .WithReference(azureServiceBus)
    .WithReference(sqlServer)
    .WaitFor(rebuildIndexQueue)
    .WaitFor(performStitchingQueue)
    .WaitFor(createJobStateQueue)
    .WaitFor(createRecurringJobStateQueue)
    .WaitFor(updateJobStateQueue)
    .WaitFor(jobFailedQueue)
    .WaitFor(updateJobCustomStateQueue)
    .WaitFor(recurringJobsQueue)
    .WaitFor(startRecurringJobQueue)
    .WaitFor(sqlServer);

builder.AddProject<Projects.Api>("api")
    .WithReference(azureServiceBus)
    .WithReference(sqlServer)
    .WaitFor(performStitchingQueue);

builder.Build().Run();
