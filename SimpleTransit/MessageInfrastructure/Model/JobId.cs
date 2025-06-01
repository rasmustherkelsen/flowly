namespace SimpleTransit.MessageInfrastructure.Model;

public record JobId(Guid InnerId)
{
    public override string ToString() => InnerId.ToString();
}