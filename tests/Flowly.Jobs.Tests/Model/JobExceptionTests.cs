using Flowly.Jobs.Model;

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
}
