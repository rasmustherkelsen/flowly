namespace Flowly.Jobs.Tests;

public class JobQueuesNamesTests
{
    public class Constants
    {
        [Fact]
        public void CreateJobState_UsesFlowlysysPrefix()
        {
            Assert.Equal("flowlysys-create-job-state", JobQueuesNames.CreateJobState);
        }

        [Fact]
        public void CreateRecurringJobState_UsesFlowlysysPrefix()
        {
            Assert.Equal("flowlysys-create-recurring-job-state", JobQueuesNames.CreateRecurringJobState);
        }

        [Fact]
        public void UpdateJobState_UsesFlowlysysPrefix()
        {
            Assert.Equal("flowlysys-update-job-state", JobQueuesNames.UpdateJobState);
        }

        [Fact]
        public void JobFailed_UsesFlowlysysPrefix()
        {
            Assert.Equal("flowlysys-job-failed", JobQueuesNames.JobFailed);
        }

        [Fact]
        public void UpdateJobCustomState_UsesFlowlysysPrefix()
        {
            Assert.Equal("flowlysys-update-job-custom-state", JobQueuesNames.UpdateJobCustomState);
        }

        [Fact]
        public void RecurringJobs_UsesFlowlysysPrefix()
        {
            Assert.Equal("flowlysys-recurring-jobs", JobQueuesNames.RecurringJobs);
        }

        [Fact]
        public void StartRecurringJob_UsesFlowlysysPrefix()
        {
            Assert.Equal("flowlysys-start-recurring-job", JobQueuesNames.StartRecurringJob);
        }

        [Fact]
        public void JobIsAlive_UsesFlowlysysPrefix()
        {
            Assert.Equal("flowlysys-job-is-alive", JobQueuesNames.JobIsAlive);
        }

        [Fact]
        public void AllConstants_AreUnique()
        {
            var allNames = new[]
            {
                JobQueuesNames.CreateJobState,
                JobQueuesNames.CreateRecurringJobState,
                JobQueuesNames.UpdateJobState,
                JobQueuesNames.JobFailed,
                JobQueuesNames.UpdateJobCustomState,
                JobQueuesNames.RecurringJobs,
                JobQueuesNames.StartRecurringJob,
                JobQueuesNames.JobIsAlive
            };

            Assert.Equal(allNames.Length, allNames.Distinct().Count());
        }
    }
}
