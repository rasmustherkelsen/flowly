namespace Flowly.Jobs.Model;

public record JobId
{
    public JobId()
    {
        InnerId = Guid.NewGuid();
    }

    public JobId(Guid innerId)
    {
        InnerId = innerId;
    }

    public override string ToString() => InnerId.ToString();
    
    public Guid InnerId { get; init; }
}