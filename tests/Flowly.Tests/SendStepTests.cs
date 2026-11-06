namespace Flowly.Tests;

public class SendStepTests
{
    public class Handle
    {
        [Fact]
        public async Task ByDefault_CallsNext()
        {
            var sendStep = new PassThroughStep();
            var nextCalled = false;

            await sendStep.Handle(
                new SendStepContext<object>(new OrderMessage(), CancellationToken.None),
                () =>
                {
                    nextCalled = true;

                    return Task.CompletedTask;
                });

            Assert.True(nextCalled);
        }

        [Fact]
        public async Task ByDefault_ReturnsTheTaskOfNext()
        {
            var sendStep = new PassThroughStep();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sendStep.Handle(
                new SendStepContext<object>(new OrderMessage(), CancellationToken.None),
                () => throw new InvalidOperationException("send failed")));

            Assert.Equal("send failed", exception.Message);
        }
    }

    private sealed class OrderMessage;

    private sealed class PassThroughStep : SendStep;
}
