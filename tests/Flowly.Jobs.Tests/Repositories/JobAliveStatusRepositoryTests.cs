using Flowly.Jobs.Model;
using Flowly.Jobs.Repositories;
using Flowly.Jobs.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.Tests.Repositories;

public class JobAliveStatusRepositoryTests
{
    public class CreateJobAliveStatus
    {
        [Fact]
        public async Task StoresJobIdentifierAndTimestamp()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobAliveStatusRepository(factory);
            var jobId = new JobId();
            var timestamp = DateTimeOffset.UtcNow;

            await repository.CreateJobAliveStatus(jobId, timestamp);

            await using var context = await factory.CreateDbContextAsync();
            var status = await context.JobAliveStatuses.SingleOrDefaultAsync(s => s.JobIdentifier == jobId.InnerId);
            Assert.NotNull(status);
            Assert.Equal(jobId.InnerId, status.JobIdentifier);
        }
    }

    public class SetJobAliveStatus
    {
        [Fact]
        public async Task UpdatesTimestampForExistingRecord()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new JobAliveStatusRepository(factory);
            var jobId = new JobId();
            var initial = DateTimeOffset.UtcNow.AddMinutes(-5);
            await repository.CreateJobAliveStatus(jobId, initial);

            var updated = DateTimeOffset.UtcNow;
            await repository.SetJobAliveStatus(jobId, updated);

            await using var context = await factory.CreateDbContextAsync();
            var status = await context.JobAliveStatuses.SingleAsync(s => s.JobIdentifier == jobId.InnerId);
            Assert.True(status.LastAliveTimestamp >= updated.AddSeconds(-1));
        }
    }
}
