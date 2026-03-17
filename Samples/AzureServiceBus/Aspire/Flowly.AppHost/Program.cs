using Flowly.AzureServiceBus.Aspire;

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

var backendProcessor = builder.AddProject<Projects.BackendProcessor>("BackendProcessor");

azureServiceBus.AddFlowly(backendProcessor);

backendProcessor
    .WithReference(azureServiceBus)
    .WithReference(flowlyJobsDatabase)
    .WaitFor(azureServiceBus)
    .WaitFor(flowlyJobsDatabase);

builder.AddProject<Projects.Api>("api")
    .WithReference(azureServiceBus)
    .WithReference(sqlServer)
    .WaitFor(azureServiceBus);

builder.Build().Run();
