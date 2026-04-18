var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("Postgres", builder.AddParameter("postgres-password", secret: true, value: "Flowly_Postgres_Pass!"))
    .WithPgAdmin();

var flowlyJobsDatabase = postgres.AddDatabase("FlowlyJobs");
var flowlyDeadLettersDatabase = postgres.AddDatabase("FlowlyDeadLetters");

var rabbitMq = builder
    .AddRabbitMQ("RabbitMQ",
        userName: builder.AddParameter("rabbitmq-username", value: "guest"),
        password: builder.AddParameter("rabbitmq-password", secret: true, value: "guest"))
    .WithManagementPlugin();

var backendProcessor = builder.AddProject<Projects.BackendProcessor>("BackendProcessor");

backendProcessor
    .WithReference(rabbitMq)
    .WithReference(flowlyJobsDatabase)
    .WithReference(flowlyDeadLettersDatabase)
    .WaitFor(rabbitMq)
    .WaitFor(flowlyJobsDatabase)
    .WaitFor(flowlyDeadLettersDatabase);

var backendFinanceProcessor = builder.AddProject<Projects.BackendFinanceProcessor>("BackendFinanceProcessor");

backendFinanceProcessor
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq);

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(rabbitMq)
    .WithReference(flowlyJobsDatabase)
    .WithReference(flowlyDeadLettersDatabase)
    .WaitFor(rabbitMq)
    .WaitFor(flowlyJobsDatabase)
    .WaitFor(flowlyDeadLettersDatabase);

builder.AddJavaScriptApp("dashboard", "../../../AzureServiceBus/Aspire/Dashboard")
    .WithNpm()
    .WithHttpEndpoint(env: "PORT")
    .WithReference(api)
    .WithExternalHttpEndpoints()
    .WaitFor(api);

builder.Build().Run();
