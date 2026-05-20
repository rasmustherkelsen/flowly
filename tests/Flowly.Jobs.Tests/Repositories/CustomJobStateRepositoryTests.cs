using System.Text.Json;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Repositories;
using Flowly.Jobs.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.Tests.Repositories;

public class CustomJobStateRepositoryTests
{
    public class CreateCustomJobState
    {
        [Fact]
        public async Task CreatesRecordWithNullState()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new CustomJobStateRepository(factory);
            var jobId = new JobId();

            await repository.CreateCustomJobState(jobId);

            await using var context = await factory.CreateDbContextAsync();
            var record = await context.CustomJobStates.SingleOrDefaultAsync(s => s.JobIdentifier == jobId.InnerId);
            Assert.NotNull(record);
            Assert.Null(record.CustomState);
        }
    }

    public class UpdateJobCustomState
    {
        [Fact]
        public async Task SerializesCustomStateToJson()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new CustomJobStateRepository(factory);
            var jobId = new JobId();
            await repository.CreateCustomJobState(jobId);

            var state = new { Progress = 42, Status = "running" };
            await repository.UpdateJobCustomState(new UpdateCustomJobState(jobId, state));

            await using var context = await factory.CreateDbContextAsync();
            var record = await context.CustomJobStates.SingleAsync(s => s.JobIdentifier == jobId.InnerId);
            Assert.NotNull(record.CustomState);
            var parsed = JsonSerializer.Deserialize<JsonElement>(record.CustomState);
            Assert.Equal(42, parsed.GetProperty("Progress").GetInt32());
        }

        [Fact]
        public async Task UpdatesExistingRecord()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new CustomJobStateRepository(factory);
            var jobId = new JobId();
            await repository.CreateCustomJobState(jobId);
            await repository.UpdateJobCustomState(new UpdateCustomJobState(jobId, new { Step = 1 }));

            await repository.UpdateJobCustomState(new UpdateCustomJobState(jobId, new { Step = 2 }));

            await using var context = await factory.CreateDbContextAsync();
            var records = await context.CustomJobStates.Where(s => s.JobIdentifier == jobId.InnerId).ToListAsync();
            var record = Assert.Single(records);
            var parsed = JsonSerializer.Deserialize<JsonElement>(record.CustomState!);
            Assert.Equal(2, parsed.GetProperty("Step").GetInt32());
        }
    }
}
