using Flowly.MessageInfrastructure.SendSteps;

namespace Flowly.Tests.MessageInfrastructure.SendSteps;

public class SendStepRegistrationTests
{
    public class ResolveFilterType
    {
        [Fact]
        public void ForNonGenericStep_ReturnsObject()
        {
            Assert.Equal(typeof(object), SendStepRegistration.ResolveFilterType(typeof(EveryMessageStep)));
        }

        [Fact]
        public void ForInterfaceFilteredStep_ReturnsInterface()
        {
            Assert.Equal(typeof(IRequiresAuthorization), SendStepRegistration.ResolveFilterType(typeof(AuthorizationStep)));
        }

        [Fact]
        public void ForBaseClassFilteredStep_ReturnsBaseClass()
        {
            Assert.Equal(typeof(BaseMessage), SendStepRegistration.ResolveFilterType(typeof(BaseMessageStep)));
        }

        [Fact]
        public void ForStepDerivedFromAnotherStep_ReturnsFilterOfTheBaseStep()
        {
            Assert.Equal(typeof(IRequiresAuthorization), SendStepRegistration.ResolveFilterType(typeof(SpecializedAuthorizationStep)));
        }

        [Fact]
        public void ForTypeWithoutSendStepBase_ThrowsArgumentExceptionNamingTheTypeAndSendStep()
        {
            var exception = Assert.Throws<ArgumentException>(() => SendStepRegistration.ResolveFilterType(typeof(NotDerivedStep)));

            Assert.Contains(nameof(NotDerivedStep), exception.Message);
            Assert.Contains("send step", exception.Message);
        }
    }

    private interface IRequiresAuthorization;

    private class BaseMessage;

    private class AuthorizationStep : SendStep<IRequiresAuthorization>;

    private sealed class SpecializedAuthorizationStep : AuthorizationStep;

    private sealed class BaseMessageStep : SendStep<BaseMessage>;

    private sealed class EveryMessageStep : SendStep;

    private sealed class NotDerivedStep : ISendStep;
}
