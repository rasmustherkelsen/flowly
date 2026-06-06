using Flowly.Jobs;
using MyAspireApp.App.Messages;

namespace MyAspireApp.App.Handlers;

internal class ProcessJobHandler(ILogger<ProcessJobHandler> logger) : JobHandler<ProcessJobMessage>
{
    public override async Task Handle(IJobMessageContext<ProcessJobMessage> messageContext)
    {
        logger.LogInformation("Starting: {Description}", messageContext.Message.Description);

        await messageContext.SaveState(new { ProgressPercentage = 0 });
        await Task.Delay(TimeSpan.FromSeconds(3), messageContext.CancellationToken);

        await messageContext.SaveState(new { ProgressPercentage = 50 });
        logger.LogInformation("Halfway through: {Description}", messageContext.Message.Description);
        await Task.Delay(TimeSpan.FromSeconds(3), messageContext.CancellationToken);

        await messageContext.SaveState(new { ProgressPercentage = 100 });
        logger.LogInformation("Completed: {Description}", messageContext.Message.Description);
    }
}
