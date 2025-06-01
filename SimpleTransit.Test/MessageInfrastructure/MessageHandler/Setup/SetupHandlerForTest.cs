using AutoFixture;
using AutoFixture.AutoNSubstitute;
using SimpleTransit.Repositories;

namespace SimpleTransit.Test.MessageInfrastructure.MessageHandler.Setup;

internal class SetupHandlerForTest : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize(new AutoNSubstituteCustomization());
        fixture.Inject(fixture.Create<IJobStateRepository>());
    }
}