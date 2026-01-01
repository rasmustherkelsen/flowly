namespace SimpleTransit.MessagingAbstractions;

public record ErrorDetails(Exception Exception, string EndPoint);