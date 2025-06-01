namespace SimpleTransit.MessageInfrastructure.Model;

public interface IJobMessage
{
    public string Description { get; }

    public string JobTypeName { get; }
}