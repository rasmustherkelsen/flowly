using Flowly.MessageInfrastructure.Callers;

namespace Flowly.Tests.MessageInfrastructure.Callers;

public class PendingCallRegistryTests
{
    public class Register
    {
        [Fact]
        public async Task ReturnsCompletedTaskWhenResolved()
        {
            var registry = new PendingCallRegistry<string>();

            var task = registry.Register("corr-1", CancellationToken.None);
            registry.TryResolve("corr-1", "hello");

            var result = await task;

            Assert.Equal("hello", result);
        }

        [Fact]
        public async Task CancelledTokenCancelsReturnedTask()
        {
            var registry = new PendingCallRegistry<string>();
            using var cts = new CancellationTokenSource();

            var task = registry.Register("corr-1", cts.Token);
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        }

        [Fact]
        public async Task MultipleCorrelationIdsAreIndependent()
        {
            var registry = new PendingCallRegistry<string>();

            var task1 = registry.Register("corr-1", CancellationToken.None);
            var task2 = registry.Register("corr-2", CancellationToken.None);

            registry.TryResolve("corr-2", "second");
            registry.TryResolve("corr-1", "first");

            Assert.Equal("first", await task1);
            Assert.Equal("second", await task2);
        }
    }

    public class TryResolve
    {
        [Fact]
        public void ReturnsTrueWhenPendingCallExists()
        {
            var registry = new PendingCallRegistry<string>();
            registry.Register("corr-1", CancellationToken.None);

            var resolved = registry.TryResolve("corr-1", "result");

            Assert.True(resolved);
        }

        [Fact]
        public void ReturnsFalseWhenNoMatchingCorrelationId()
        {
            var registry = new PendingCallRegistry<string>();

            var resolved = registry.TryResolve("nonexistent", "result");

            Assert.False(resolved);
        }

        [Fact]
        public void ReturnsFalseWhenAlreadyResolved()
        {
            var registry = new PendingCallRegistry<string>();
            registry.Register("corr-1", CancellationToken.None);
            registry.TryResolve("corr-1", "first");

            var resolvedAgain = registry.TryResolve("corr-1", "second");

            Assert.False(resolvedAgain);
        }

        [Fact]
        public void ReturnsFalseAfterCancellation()
        {
            var registry = new PendingCallRegistry<string>();
            using var cts = new CancellationTokenSource();

            registry.Register("corr-1", cts.Token);
            cts.Cancel();

            var resolved = registry.TryResolve("corr-1", "late response");

            Assert.False(resolved);
        }
    }
}
