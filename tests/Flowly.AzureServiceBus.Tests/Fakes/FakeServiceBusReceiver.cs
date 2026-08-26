using Azure.Messaging.ServiceBus;

namespace Flowly.AzureServiceBus.Tests.Fakes;

internal class FakeServiceBusReceiver : ServiceBusReceiver
{
    public int CompleteMessageCallCount { get; private set; }
    public int DeadLetterMessageCallCount { get; private set; }
    public Exception? ExceptionToThrowOnComplete { get; set; }
    public Exception? ExceptionToThrowOnDeadLetter { get; set; }

    public override Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        CompleteMessageCallCount++;
        if (ExceptionToThrowOnComplete is not null) throw ExceptionToThrowOnComplete;
        return Task.CompletedTask;
    }

    public override Task DeadLetterMessageAsync(ServiceBusReceivedMessage message, string deadLetterReason, string? deadLetterErrorDescription = null, CancellationToken cancellationToken = default)
    {
        DeadLetterMessageCallCount++;
        if (ExceptionToThrowOnDeadLetter is not null) throw ExceptionToThrowOnDeadLetter;
        return Task.CompletedTask;
    }
}
