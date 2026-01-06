namespace Flowly.Jobs.Model;

public record JobId(Guid InnerId)
{
    public override string ToString() => InnerId.ToString();
}