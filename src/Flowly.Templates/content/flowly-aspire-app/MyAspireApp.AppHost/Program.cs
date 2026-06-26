#if (UseAzureServiceBus)
using Flowly.AzureServiceBus.Aspire;

#endif
var builder = DistributedApplication.CreateBuilder(args);

#if (UseRabbitMQ)
var rabbitMq = builder
    .AddRabbitMQ("RabbitMQ",
        userName: builder.AddParameter("rabbitmq-username", value: "guest"),
        password: builder.AddParameter("rabbitmq-password", secret: true, value: "guest"))
    .WithManagementPlugin();
#endif
#if (UseAzureServiceBus)
var azureServiceBus = builder
    .AddAzureServiceBus("AzureServiceBus")
    .RunAsEmulator();
#endif
#if (NeedsSqlServerInfrastructure)
var sqlServer = builder
    .AddSqlServer("SqlServer", builder.AddParameter("sql-password", secret: true, value: "Fl0wly_Dev_Pass!"))
    .WithDataVolume();
#endif
#if (NeedsPostgresInfrastructure)
var postgres = builder
    .AddPostgres("Postgres", null, builder.AddParameter("postgres-password", secret: true, value: "Fl0wly_Dev_Pass!"));
#endif
#if (UseJobTracking && UseSqlServer)
var flowlyJobsDb = sqlServer.AddDatabase("FlowlyJobs");
#endif
#if (UseJobTracking && UsePostgres)
var flowlyJobsDb = postgres.AddDatabase("FlowlyJobs");
#endif
#if (UseDeadLetterTracking && UseSqlServer)
var flowlyDeadLettersDb = sqlServer.AddDatabase("FlowlyDeadLetters");
#endif
#if (UseDeadLetterTracking && UsePostgres)
var flowlyDeadLettersDb = postgres.AddDatabase("FlowlyDeadLetters");
#endif

#if (!UseInMemory)
var sender = builder.AddProject<Projects.MyAspireApp_Sender>("sender");
var receiver = builder.AddProject<Projects.MyAspireApp_Receiver>("receiver");
#if (UseJobTracking)
var jobTracker = builder.AddProject<Projects.MyAspireApp_JobTracker>("job-tracker");
#endif
#if (UseDeadLetterTracking)
var deadLetterTracker = builder.AddProject<Projects.MyAspireApp_DeadLetterTracker>("dead-letter-tracker");
#endif
#if (UseDashboard)
var dashboard = builder.AddProject<Projects.MyAspireApp_Dashboard>("dashboard");
#endif
#if (UseAzureServiceBus)

azureServiceBus.AddFlowly(receiver);
#if (UseCallHandler)
azureServiceBus.AddFlowly(sender);
#endif
#if (UseJobTracking)
azureServiceBus.AddFlowly(jobTracker);
#endif
#if (UseDeadLetterTracking)
azureServiceBus.AddFlowly(deadLetterTracker);
#endif
#if (UseDashboard)
azureServiceBus.AddFlowly(dashboard);
#endif
#endif

#if (UseRabbitMQ)
sender.WithReference(rabbitMq).WaitFor(rabbitMq);
#endif
#if (UseAzureServiceBus)
sender.WithReference(azureServiceBus).WaitFor(azureServiceBus);
#endif
#if (UseRabbitMQ)
receiver.WithReference(rabbitMq).WaitFor(rabbitMq);
#endif
#if (UseAzureServiceBus)
receiver.WithReference(azureServiceBus).WaitFor(azureServiceBus);
#endif
#if (UseJobTracking)

#if (UseRabbitMQ)
jobTracker.WithReference(rabbitMq).WaitFor(rabbitMq);
#endif
#if (UseAzureServiceBus)
jobTracker.WithReference(azureServiceBus).WaitFor(azureServiceBus);
#endif
#if (UseSqlServer || UsePostgres)
jobTracker.WithReference(flowlyJobsDb).WaitFor(flowlyJobsDb);
#endif
#endif
#if (UseDeadLetterTracking)

#if (UseRabbitMQ)
deadLetterTracker.WithReference(rabbitMq).WaitFor(rabbitMq);
#endif
#if (UseAzureServiceBus)
deadLetterTracker.WithReference(azureServiceBus).WaitFor(azureServiceBus);
#endif
#if (UseSqlServer || UsePostgres)
deadLetterTracker.WithReference(flowlyDeadLettersDb).WaitFor(flowlyDeadLettersDb);
#endif
#endif
#if (UseDashboard)

#if (UseRabbitMQ)
dashboard.WithReference(rabbitMq).WaitFor(rabbitMq);
#endif
#if (UseAzureServiceBus)
dashboard.WithReference(azureServiceBus).WaitFor(azureServiceBus);
#endif
#if (UseJobTracking && (UseSqlServer || UsePostgres))
dashboard.WithReference(flowlyJobsDb).WaitFor(flowlyJobsDb);
#endif
#if (UseDeadLetterTracking && (UseSqlServer || UsePostgres))
dashboard.WithReference(flowlyDeadLettersDb).WaitFor(flowlyDeadLettersDb);
#endif
#endif
#endif
#if (UseInMemory)
var app = builder.AddProject<Projects.MyAspireApp_App>("app");

#if (UseJobTracking && (UseSqlServer || UsePostgres))
app.WithReference(flowlyJobsDb).WaitFor(flowlyJobsDb);
#endif
#if (UseDeadLetterTracking && (UseSqlServer || UsePostgres))
app.WithReference(flowlyDeadLettersDb).WaitFor(flowlyDeadLettersDb);
#endif
#endif

builder.Build().Run();
