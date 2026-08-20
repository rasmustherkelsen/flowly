using Flowly;
using Flowly.InMemory;
#if (UseJobTracking)
using Flowly.Jobs;
#endif
using App.Handlers;
using App.Messages;

namespace App;

internal class FlowlyConfiguration : Configuration
{
#if (UseCallHandler)
    public override string? InstanceName => "app";

#endif
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseInMemory();
#if (UseJobTracking && UseSqlServer)

        builder.AddSqlServerJobStateTracking("FlowlyJobs", enableMigrations: true);
#endif
#if (UseJobTracking && UsePostgres)

        builder.AddPostgresJobStateTracking("FlowlyJobs", enableMigrations: true);
#endif
#if (UseJobTracking && UseSQLite)

        builder.AddSQLiteJobStateTracking("FlowlyJobs", enableMigrations: true);
#endif
#if (UseDeadLetterTracking && UseSqlServer)

        builder.AddSqlServerDeadLetterTracking("FlowlyDeadLetters",enableMigrations: true);
#endif
#if (UseDeadLetterTracking && UsePostgres)

        builder.AddPostgresDeadLetterTracking("FlowlyDeadLetters",enableMigrations: true);
#endif
#if (UseDeadLetterTracking && UseSQLite)

        builder.AddSQLiteDeadLetterTracking("FlowlyDeadLetters",enableMigrations: true);
#endif

#if (UseCallHandler)
        builder.AddCallSubmitter<MyMessage>();
        builder.AddCallHandler<MyMessage, MyMessageHandler>();
#else
#if (UseStream)
        builder.AddMessageRecorder<MyMessage>();
        builder.AddMessageStreamHandler<MyMessage, MyMessageHandler>();
#else
        builder.AddMessageSubmitter<MyMessage>();
        builder.AddMessageHandler<MyMessage, MyMessageHandler>();
#endif
#endif
#if (UseJobTracking)

        builder.AddJobSubmitter<ProcessJobMessage>();
        builder.AddJobHandler<ProcessJobMessage, ProcessJobHandler>();
#endif
#if (UseDeadLetterTracking)

        builder.AddMessageSubmitter<DeadLetterSampleMessage>();
        builder.AddMessageHandler<DeadLetterSampleMessage, DeadLetterSampleMessageHandler>()
               .WithDeadLetterTracking();
#endif
    }
}
