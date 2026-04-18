using Flowly.AzureServiceBus.Aspire;
using MessageContracts;

var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder
    .AddSqlServer("SqlServer", builder.AddParameter("sql-password", secret: true, value: "6v9p}-3Y(eWz7Cqwy6-93Y"), port: 6508);

var flowlyJobsDatabase = sqlServer.AddDatabase("FlowlyJobs");
var flowlyDeadLettersDatabase = sqlServer.AddDatabase("FlowlyDeadLetters");

var azureServiceBus = builder
    .AddAzureServiceBus("EmulatorNamespace")
    .RunAsEmulator(c =>
    {
        c.WithEnvironment("MSSQL_CONNECTION_STRING", $"Server=localhost,6508;Database=AzureServiceBus;User Id=sa;Password=6v9p}}-3Y(eWz7Cqwy6-93Y;TrustServerCertificate=True");
    });

var backendProcessor = builder.AddProject<Projects.BackendProcessor>("BackendProcessor");
azureServiceBus.AddFlowly(backendProcessor);

var backendFinanceProcessor = builder.AddProject<Projects.BackendFinanceProcessor>("BackendFinanceProcessor");
azureServiceBus.AddFlowly(backendFinanceProcessor, topology =>
    topology.AddEventSubscription<OrderProcessedEvent>("finance-order-processed-event-handler"));

backendProcessor
    .WaitFor(azureServiceBus)
    .WaitFor(flowlyJobsDatabase)
    .WaitFor(flowlyDeadLettersDatabase)
    .WithReference(azureServiceBus)
    .WithReference(flowlyJobsDatabase)
    .WithReference(flowlyDeadLettersDatabase);

backendFinanceProcessor
    .WaitFor(azureServiceBus)
    .WithReference(azureServiceBus);

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(azureServiceBus)
    .WithReference(flowlyJobsDatabase)
    .WithReference(flowlyDeadLettersDatabase)
    .WaitFor(azureServiceBus)
    .WaitFor(flowlyJobsDatabase)
    .WaitFor(flowlyDeadLettersDatabase);

builder.AddJavaScriptApp("dashboard", "../Dashboard")
    .WithNpm()
    .WithHttpEndpoint(env: "PORT")
    .WithReference(api)
    .WithExternalHttpEndpoints()
    .WaitFor(api);

builder.Build().Run();
