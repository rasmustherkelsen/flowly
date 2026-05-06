using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.Model;
using Flowly.Jobs.Repositories;
using Flowly.Jobs.Senders;
using Flowly.Jobs.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Tests.BackgroundServices;

public class RecurringJobSchedulerBackgroundServiceTests
{
    public class ExecuteAsync
    {
        [Fact]
        public async Task SubmitsDueRecurringJob()
        {
            using var cts = new CancellationTokenSource();
            var jobId = Guid.NewGuid();
            var repository = new FakeJobStateRepository { RecurringJobsToReturn = [MakeDueJob(jobId)] };
            repository.OnGetRecurringJobsCalled = () => cts.Cancel();
            var invoker = new CapturingRecurringJobInvoker();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BuildService(repository, invoker).ExecuteAsync(cts.Token));

            var submitted = Assert.Single(invoker.SubmittedJobs);
            Assert.Equal(jobId, submitted.JobId);
        }

        [Fact]
        public async Task SubmitsAllDueJobsInOnePass()
        {
            using var cts = new CancellationTokenSource();
            var repository = new FakeJobStateRepository { RecurringJobsToReturn = [MakeDueJob(), MakeDueJob(), MakeDueJob()] };
            repository.OnGetRecurringJobsCalled = () => cts.Cancel();
            var invoker = new CapturingRecurringJobInvoker();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BuildService(repository, invoker).ExecuteAsync(cts.Token));

            Assert.Equal(3, invoker.SubmittedJobs.Count);
        }

        [Fact]
        public async Task SkipsCurrentlyRunningJob()
        {
            using var cts = new CancellationTokenSource();
            var repository = new FakeJobStateRepository { RecurringJobsToReturn = [MakeRunningJob()] };
            repository.OnGetRecurringJobsCalled = () => cts.Cancel();
            var invoker = new CapturingRecurringJobInvoker();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BuildService(repository, invoker).ExecuteAsync(cts.Token));

            Assert.Empty(invoker.SubmittedJobs);
        }

        [Fact]
        public async Task WithNoRecurringJobs_DoesNotCallSubmit()
        {
            using var cts = new CancellationTokenSource();
            var repository = new FakeJobStateRepository { RecurringJobsToReturn = [] };
            repository.OnGetRecurringJobsCalled = () => cts.Cancel();
            var invoker = new CapturingRecurringJobInvoker();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BuildService(repository, invoker).ExecuteAsync(cts.Token));

            Assert.Empty(invoker.SubmittedJobs);
        }

        [Fact]
        public async Task SubmitsDueJobsButNotRunningJobsFromMixedList()
        {
            using var cts = new CancellationTokenSource();
            var dueJobId = Guid.NewGuid();
            var repository = new FakeJobStateRepository { RecurringJobsToReturn = [MakeDueJob(dueJobId), MakeRunningJob()] };
            repository.OnGetRecurringJobsCalled = () => cts.Cancel();
            var invoker = new CapturingRecurringJobInvoker();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BuildService(repository, invoker).ExecuteAsync(cts.Token));

            var submitted = Assert.Single(invoker.SubmittedJobs);
            Assert.Equal(dueJobId, submitted.JobId);
        }

        [Fact]
        public async Task WithAlreadyCancelledToken_DoesNotGetRecurringJobs()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var getRecurringJobsCalled = false;
            var repository = new FakeJobStateRepository();
            repository.OnGetRecurringJobsCalled = () => getRecurringJobsCalled = true;
            var invoker = new CapturingRecurringJobInvoker();

            await BuildService(repository, invoker).ExecuteAsync(cts.Token);

            Assert.False(getRecurringJobsCalled);
        }
    }

    private static RecurringJob MakeDueJob(Guid? jobId = null) =>
        new(jobId ?? Guid.NewGuid(), "TestJob", "Test job", "* * * * *",
            DateTimeOffset.UtcNow.AddDays(-2), null, null);

    private static RecurringJob MakeRunningJob() =>
        new(Guid.NewGuid(), "TestJob", "Test job", "* * * * *",
            DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddMinutes(-5), null);

    private static TestableScheduler BuildService(FakeJobStateRepository repository, CapturingRecurringJobInvoker invoker)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJobStateRepository>(repository);
        services.AddSingleton<IRecurringJobInvoker>(invoker);
        var provider = services.BuildServiceProvider();

        return new TestableScheduler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new RecurringJobSchedulerBackgroundServiceOptions(TimeSpan.FromDays(1)));
    }

    private sealed class TestableScheduler(
        IServiceScopeFactory serviceScopeFactory,
        RecurringJobSchedulerBackgroundServiceOptions options)
        : RecurringJobSchedulerBackgroundService(serviceScopeFactory, options)
    {
        public new Task ExecuteAsync(CancellationToken cancellationToken) => base.ExecuteAsync(cancellationToken);
    }

    private sealed class CapturingRecurringJobInvoker : IRecurringJobInvoker
    {
        public List<RecurringJob> SubmittedJobs { get; } = [];

        public Task Submit(RecurringJob recurringJob)
        {
            SubmittedJobs.Add(recurringJob);
            return Task.CompletedTask;
        }
    }
}
