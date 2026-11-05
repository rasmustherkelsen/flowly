namespace Flowly.MessageInfrastructure.SendSteps;

internal interface ISendStepRunner
{
    Task Run(object message, CancellationToken cancellationToken, SendStepDelegate send);
}
