namespace Messages;

public record OrderSubmittedMessage(Guid OrderId, string Description);
