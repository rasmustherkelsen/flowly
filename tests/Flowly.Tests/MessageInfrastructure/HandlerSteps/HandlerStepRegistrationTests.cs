using Flowly.MessageInfrastructure.HandlerSteps;

namespace Flowly.Tests.MessageInfrastructure.HandlerSteps;

public class HandlerStepRegistrationTests
{
    public class ResolveFilterType
    {
        [Fact]
        public void ForNonGenericStep_ReturnsObject()
        {
            Assert.Equal(typeof(object), HandlerStepRegistration.ResolveFilterType(typeof(EveryMessageStep)));
        }

        [Fact]
        public void ForInterfaceFilteredStep_ReturnsInterface()
        {
            Assert.Equal(typeof(IEnableAuditLog), HandlerStepRegistration.ResolveFilterType(typeof(AuditStep)));
        }

        [Fact]
        public void ForBaseClassFilteredStep_ReturnsBaseClass()
        {
            Assert.Equal(typeof(BaseMessage), HandlerStepRegistration.ResolveFilterType(typeof(BaseMessageStep)));
        }

        [Fact]
        public void ForStepDerivedFromAnotherStep_ReturnsFilterOfTheBaseStep()
        {
            Assert.Equal(typeof(IEnableAuditLog), HandlerStepRegistration.ResolveFilterType(typeof(SpecializedAuditStep)));
        }

        [Fact]
        public void ForTypeWithoutHandlerStepBase_ThrowsArgumentExceptionNamingTheType()
        {
            var exception = Assert.Throws<ArgumentException>(() => HandlerStepRegistration.ResolveFilterType(typeof(NotDerivedStep)));

            Assert.Contains(nameof(NotDerivedStep), exception.Message);
        }
    }

    private interface IEnableAuditLog;

    private class BaseMessage;

    private class AuditStep : HandlerStep<IEnableAuditLog>;

    private sealed class SpecializedAuditStep : AuditStep;

    private sealed class BaseMessageStep : HandlerStep<BaseMessage>;

    private sealed class EveryMessageStep : HandlerStep;

    private sealed class NotDerivedStep : IHandlerStep;
}
