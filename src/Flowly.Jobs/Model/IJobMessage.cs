namespace Flowly.Jobs.Model;

public interface IJobMessage
{
    public string Description { get; }

    public string JobTypeName { get; }
}