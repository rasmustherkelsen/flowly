using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure;

namespace Flowly.Jobs.Tests.Model;

public class JobExceptionTests
{
    public class Constructor
    {
        [Fact]
        public void StoresProvidedJobId()
        {
            var jobId = new JobId();

            var jobException = new JobException(jobId, new InvalidOperationException());

            Assert.Equal(jobId, jobException.JobId);
        }

        [Fact]
        public void MessageContainsJobId()
        {
            var jobId = new JobId();

            var jobException = new JobException(jobId, new InvalidOperationException());

            Assert.Contains(jobId.ToString(), jobException.Message);
        }

        [Fact]
        public void WrapsInnerException()
        {
            var innerException = new InvalidOperationException("original cause");

            var jobException = new JobException(new JobId(), innerException);

            Assert.Same(innerException, jobException.InnerException);
        }
    }

    public class WrappedException
    {
        [Fact]
        public void ReturnsTheWrappedHandlerException()
        {
            var handlerException = new InvalidOperationException("original cause");

            var jobException = new JobException(new JobId(), handlerException);

            Assert.Same(handlerException, jobException.WrappedException);
        }

        [Fact]
        public void LetsClassifierSeeRejectionThroughJobException()
        {
            var nonRetryableExceptionClassifier = new NonRetryableExceptionClassifier(new FlowlyOptions());

            var jobException = new JobException(new JobId(), new MessageRejectedException("not valid"));

            Assert.True(nonRetryableExceptionClassifier.IsNonRetryable(jobException));
        }
    }
}
