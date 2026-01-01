namespace SimpleTransit.MessagingAbstractions;

public enum MessageBusReceiveMode
{
    PeekLock,
    ReceiveAndDelete
}