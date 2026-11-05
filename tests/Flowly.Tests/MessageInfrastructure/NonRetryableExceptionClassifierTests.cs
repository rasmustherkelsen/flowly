using Flowly.MessageInfrastructure;

namespace Flowly.Tests.MessageInfrastructure;

public class NonRetryableExceptionClassifierTests
{
    public class IsNonRetryable
    {
        [Fact]
        public void WithMessageRejectedException_ReturnsTrue()
        {
            var nonRetryableExceptionClassifier = new NonRetryableExceptionClassifier(new FlowlyOptions());

            Assert.True(nonRetryableExceptionClassifier.IsNonRetryable(new MessageRejectedException("no")));
        }

        [Fact]
        public void WithSubclassOfMessageRejectedException_ReturnsTrue()
        {
            var nonRetryableExceptionClassifier = new NonRetryableExceptionClassifier(new FlowlyOptions());

            Assert.True(nonRetryableExceptionClassifier.IsNonRetryable(new DerivedRejectedException("no")));
        }

        [Fact]
        public void WithRegisteredExceptionType_ReturnsTrue()
        {
            var flowlyOptions = new FlowlyOptions();
            flowlyOptions.NonRetryable<NotAuthorizedException>();
            var nonRetryableExceptionClassifier = new NonRetryableExceptionClassifier(flowlyOptions);

            Assert.True(nonRetryableExceptionClassifier.IsNonRetryable(new NotAuthorizedException()));
        }

        [Fact]
        public void WithSubclassOfRegisteredExceptionType_ReturnsTrue()
        {
            var flowlyOptions = new FlowlyOptions();
            flowlyOptions.NonRetryable<NotAuthorizedException>();
            var nonRetryableExceptionClassifier = new NonRetryableExceptionClassifier(flowlyOptions);

            Assert.True(nonRetryableExceptionClassifier.IsNonRetryable(new ExpiredTokenException()));
        }

        [Fact]
        public void WithRejectedExceptionWrappedThroughWrapper_ReturnsTrue()
        {
            var nonRetryableExceptionClassifier = new NonRetryableExceptionClassifier(new FlowlyOptions());

            Assert.True(nonRetryableExceptionClassifier.IsNonRetryable(new Wrapper(new MessageRejectedException("no"))));
        }

        [Fact]
        public void WithRegisteredExceptionWrappedThroughNestedWrappers_ReturnsTrue()
        {
            var flowlyOptions = new FlowlyOptions();
            flowlyOptions.NonRetryable<NotAuthorizedException>();
            var nonRetryableExceptionClassifier = new NonRetryableExceptionClassifier(flowlyOptions);

            Assert.True(nonRetryableExceptionClassifier.IsNonRetryable(new Wrapper(new Wrapper(new NotAuthorizedException()))));
        }

        [Fact]
        public void WithUnrelatedException_ReturnsFalse()
        {
            var flowlyOptions = new FlowlyOptions();
            flowlyOptions.NonRetryable<NotAuthorizedException>();
            var nonRetryableExceptionClassifier = new NonRetryableExceptionClassifier(flowlyOptions);

            Assert.False(nonRetryableExceptionClassifier.IsNonRetryable(new InvalidOperationException("boom")));
        }

        [Fact]
        public void WithUnrelatedExceptionWrappedThroughWrapper_ReturnsFalse()
        {
            var nonRetryableExceptionClassifier = new NonRetryableExceptionClassifier(new FlowlyOptions());

            Assert.False(nonRetryableExceptionClassifier.IsNonRetryable(new Wrapper(new InvalidOperationException("boom"))));
        }

        [Fact]
        public void WithRegisteredExceptionAsInnerExceptionOfOrdinaryException_ReturnsFalse()
        {
            var flowlyOptions = new FlowlyOptions();
            flowlyOptions.NonRetryable<NotAuthorizedException>();
            var nonRetryableExceptionClassifier = new NonRetryableExceptionClassifier(flowlyOptions);

            Assert.False(nonRetryableExceptionClassifier.IsNonRetryable(new InvalidOperationException("boom", new NotAuthorizedException())));
        }
    }

    private class NotAuthorizedException : Exception;

    private sealed class ExpiredTokenException : NotAuthorizedException;

    private sealed class DerivedRejectedException(string reason) : MessageRejectedException(reason);

    private sealed class Wrapper(Exception wrapped) : Exception("wrapper", wrapped), IHandlerExceptionWrapper
    {
        public Exception WrappedException { get; } = wrapped;
    }
}
