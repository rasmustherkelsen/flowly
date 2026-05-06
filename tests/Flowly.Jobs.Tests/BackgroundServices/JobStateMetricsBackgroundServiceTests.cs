using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.Repositories;
using Flowly.Jobs.Telemetry;

namespace Flowly.Jobs.Tests.BackgroundServices;

public class JobStateMetricsBackgroundServiceTests
{
    public class ExecuteAsync
    {
        [Fact]
        public async Task WithTelemetryDisabled_ReturnsWithoutQueryingDatabase()
        {
            var countReader = new CapturingJobStateCountReader();
            var service = BuildService(countReader, new CapturingJobStateMetrics(), new FlowlyOptions { EnableTelemetry = false });

            await service.ExecuteAsync(CancellationToken.None);

            Assert.Empty(countReader.QueriedStates);
        }

        [Fact]
        public async Task WithTelemetryDisabled_ReturnsWithoutUpdatingMetrics()
        {
            var metrics = new CapturingJobStateMetrics();
            var service = BuildService(new CapturingJobStateCountReader(), metrics, new FlowlyOptions { EnableTelemetry = false });

            await service.ExecuteAsync(CancellationToken.None);

            Assert.Empty(metrics.RecordedCounts);
        }

        [Fact]
        public async Task WithTelemetryEnabled_QueriesFailedAndStartedJobCounts()
        {
            using var cts = new CancellationTokenSource();
            var countReader = new CapturingJobStateCountReader { FailedCount = 3, StartedCount = 7 };
            var metrics = new CapturingJobStateMetrics();
            metrics.OnUpdateCounts = () => cts.Cancel();
            var service = BuildService(countReader, metrics);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteAsync(cts.Token));

            Assert.Contains(JobState.Failed, countReader.QueriedStates);
            Assert.Contains(JobState.Started, countReader.QueriedStates);
        }

        [Fact]
        public async Task WithTelemetryEnabled_UpdatesMetricsWithCorrectCounts()
        {
            using var cts = new CancellationTokenSource();
            var countReader = new CapturingJobStateCountReader { FailedCount = 3, StartedCount = 7 };
            var metrics = new CapturingJobStateMetrics();
            metrics.OnUpdateCounts = () => cts.Cancel();
            var service = BuildService(countReader, metrics);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteAsync(cts.Token));

            var (failed, running) = Assert.Single(metrics.RecordedCounts);
            Assert.Equal(3, failed);
            Assert.Equal(7, running);
        }

        [Fact]
        public async Task WhenCountReaderThrowsAndTokenNotCancelled_SwallowsExceptionAndContinues()
        {
            using var cts = new CancellationTokenSource();
            var callCount = 0;
            var countReader = new CapturingJobStateCountReader();
            countReader.OnCountJobsInState = _ =>
            {
                callCount++;
                if (callCount == 1) throw new InvalidOperationException("db failure");
                cts.Cancel();
                return Task.FromResult(0L);
            };
            var service = BuildService(countReader, new CapturingJobStateMetrics());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteAsync(cts.Token));

            Assert.True(callCount >= 2);
        }

        [Fact]
        public async Task WhenCancelledDuringQuery_PropagatesOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            var countReader = new CapturingJobStateCountReader();
            countReader.OnCountJobsInState = _ =>
            {
                cts.Cancel();
                cts.Token.ThrowIfCancellationRequested();
                return Task.FromResult(0L);
            };
            var service = BuildService(countReader, new CapturingJobStateMetrics());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteAsync(cts.Token));
        }

        private static TestableService BuildService(
            IJobStateCountReader countReader,
            IJobStateMetrics metrics,
            FlowlyOptions? options = null) =>
            new(
                countReader,
                metrics,
                options ?? new FlowlyOptions { EnableTelemetry = true },
                new JobStateMetricsBackgroundServiceOptions(TimeSpan.FromMilliseconds(1)));
    }

    private sealed class TestableService(
        IJobStateCountReader countReader,
        IJobStateMetrics metrics,
        FlowlyOptions options,
        JobStateMetricsBackgroundServiceOptions serviceOptions)
        : JobStateMetricsBackgroundService(countReader, metrics, options, serviceOptions)
    {
        public new Task ExecuteAsync(CancellationToken cancellationToken) => base.ExecuteAsync(cancellationToken);
    }

    private sealed class CapturingJobStateCountReader : IJobStateCountReader
    {
        public long FailedCount { get; set; }
        public long StartedCount { get; set; }
        public List<JobState> QueriedStates { get; } = [];
        public Func<JobState, Task<long>>? OnCountJobsInState { get; set; }

        public Task<long> CountJobsInState(JobState state, CancellationToken cancellationToken)
        {
            if (OnCountJobsInState != null) return OnCountJobsInState(state);

            QueriedStates.Add(state);
            return Task.FromResult(state == JobState.Failed ? FailedCount : StartedCount);
        }
    }

    private sealed class CapturingJobStateMetrics : IJobStateMetrics
    {
        public List<(long Failed, long Running)> RecordedCounts { get; } = [];
        public Action? OnUpdateCounts { get; set; }

        public void UpdateCounts(long failed, long running)
        {
            RecordedCounts.Add((failed, running));
            OnUpdateCounts?.Invoke();
        }
    }
}
