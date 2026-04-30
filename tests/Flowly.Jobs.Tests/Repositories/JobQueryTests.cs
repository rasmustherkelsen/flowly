using Flowly.Jobs.Repositories;

namespace Flowly.Jobs.Tests.Repositories;

public class JobQueryTests
{
    public class ByJobType
    {
        [Fact]
        public void SetsJobTypeAndLeavesJobIdNull()
        {
            var jobQuery = JobQuery.ByJobType("MyJob");

            Assert.Equal("MyJob", jobQuery.JobType);
            Assert.Null(jobQuery.JobId);
        }

        [Fact]
        public void WithEmptyString_SetsJobTypeToEmpty()
        {
            var jobQuery = JobQuery.ByJobType("");

            Assert.Equal("", jobQuery.JobType);
        }
    }

    public class ById
    {
        [Fact]
        public void SetsJobIdAndLeavesJobTypeNull()
        {
            var id = Guid.NewGuid();

            var jobQuery = JobQuery.ById(id);

            Assert.Equal(id, jobQuery.JobId);
            Assert.Null(jobQuery.JobType);
        }

        [Fact]
        public void WithEmptyGuid_SetsJobIdToEmpty()
        {
            var jobQuery = JobQuery.ById(Guid.Empty);

            Assert.Equal(Guid.Empty, jobQuery.JobId);
        }
    }

    public class DefaultConstruction
    {
        [Fact]
        public void LeavesBothPropertiesNull()
        {
            var jobQuery = new JobQuery();

            Assert.Null(jobQuery.JobId);
            Assert.Null(jobQuery.JobType);
        }
    }
}
