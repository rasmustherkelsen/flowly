var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder
    .AddSqlServer("SqlServer", builder.AddParameter("sql-password", secret: true, value: "6v9p}-3Y(eWz7Cqwy6-93Y"), port: 6508)
    .WithLifetime(ContainerLifetime.Persistent);

var flowlyJobsDatabase = sqlServer.AddDatabase("FlowlyJobs");

var azureServiceBus = builder
    .AddAzureServiceBus("EmulatorNamespace")
    .RunAsEmulator(c =>
    {
        c.WithLifetime(ContainerLifetime.Persistent);
        c.WithEnvironment("MSSQL_CONNECTION_STRING", $"Server=localhost,6508;Database=AzureServiceBus;User Id=sa;Password=6v9p}}-3Y(eWz7Cqwy6-93Y;TrustServerCertificate=True");
    });

var flowlysysCreateJobStateQueue = azureServiceBus.AddServiceBusQueue("flowlysys-create-job-state");
var flowlysysCreateRecurringJobStateQueue = azureServiceBus.AddServiceBusQueue("flowlysys-create-recurring-job-state");
var flowlysysJobFailedQueue = azureServiceBus.AddServiceBusQueue("flowlysys-job-failed");
var flowlysysJobIsAliveQueue = azureServiceBus.AddServiceBusQueue("flowlysys-job-is-alive");
var flowlysysRecurringJobsQueue = azureServiceBus.AddServiceBusQueue("flowlysys-recurring-jobs");
var flowlysysStartRecurringJobQueue = azureServiceBus.AddServiceBusQueue("flowlysys-start-recurring-job");
var flowlysysUpdateJobCustomStateQueue = azureServiceBus.AddServiceBusQueue("flowlysys-update-job-custom-state");
var flowlysysUpdateJobStateQueue = azureServiceBus.AddServiceBusQueue("flowlysys-update-job-state");
var processOrderQueue = azureServiceBus.AddServiceBusQueue("process-order");
var rebuildIndexQueue = azureServiceBus.AddServiceBusQueue("rebuild-index");
var someQueryQueue = azureServiceBus.AddServiceBusQueue("some-query");
rebuildIndexQueue.WithProperties(q => { q.LockDuration = TimeSpan.FromMinutes(5); });

// *******************

IResourceBuilder<ProjectResource> project = builder.AddProject<Projects.BackendProcessor>("BackendProcessor")
    .WithReference(azureServiceBus)
    .WithReference(flowlyJobsDatabase)
    .WaitFor(rebuildIndexQueue)
    .WaitFor(processOrderQueue)
    .WaitFor(someQueryQueue)
    .WaitFor(flowlysysCreateJobStateQueue)
    .WaitFor(flowlysysCreateRecurringJobStateQueue)
    .WaitFor(flowlysysJobFailedQueue)
    .WaitFor(flowlysysJobIsAliveQueue)
    .WaitFor(flowlysysRecurringJobsQueue)
    .WaitFor(flowlysysStartRecurringJobQueue)
    .WaitFor(flowlysysUpdateJobCustomStateQueue)
    .WaitFor(flowlysysUpdateJobStateQueue)
    .WaitFor(flowlyJobsDatabase);

builder.AddProject<Projects.Api>("api")
    .WithReference(azureServiceBus)
    .WithReference(sqlServer)
    .WaitFor(processOrderQueue);

builder.Build().Run();