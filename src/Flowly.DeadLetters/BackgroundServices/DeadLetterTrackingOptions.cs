namespace Flowly.DeadLetters.BackgroundServices;

public class DeadLetterTrackingOptions
{
    public TimeSpan? DeleteRequeuedMessagesAfter { get; set; }

    public TimeSpan? DeleteDeadLetteredMessagesAfter { get; set; }
}
