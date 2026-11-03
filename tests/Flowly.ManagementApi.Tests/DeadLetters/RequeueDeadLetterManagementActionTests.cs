using Flowly.DeadLetters;
using Flowly.ManagementApi.DeadLetters;
using Flowly.ManagementApi.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Flowly.ManagementApi.Tests.DeadLetters;

public class RequeueDeadLetterManagementActionTests
{
    public class Properties
    {
        [Fact]
        public void FeatureAreaIsDeadLetters()
        {
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            Assert.Equal("dead-letters", requeueDeadLetterManagementAction.FeatureArea);
        }

        [Fact]
        public void RequiresTheWriteCapability()
        {
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            Assert.Equal(ManagementCapability.Write, requeueDeadLetterManagementAction.RequiredCapability);
        }
    }

    public class Requeue
    {
        [Fact]
        public async Task ForAPendingDeadLetter_Returns204AndRequeuesWithTheRequester()
        {
            var deadLetterService = new FakeDeadLetterService();
            var deadLetter = deadLetterService.Add("m1");
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            var result = await requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest("m1", "ops"), CancellationToken.None);

            Assert.IsType<NoContent>(result);
            Assert.Equal(("m1", "ops"), Assert.Single(deadLetterService.RequeueCalls));
            Assert.Equal(DeadLetterStatus.Requeued, deadLetter.Status);
        }

        [Fact]
        public async Task ForAnUnknownMessageId_Returns404WithAProblemBody()
        {
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            var result = Assert.IsType<ProblemHttpResult>(await requeueDeadLetterManagementAction.Requeue(new FakeDeadLetterService(), new RequeueDeadLetterRequest("missing", null), CancellationToken.None));

            Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
            Assert.Contains("missing", result.ProblemDetails.Detail);
        }

        [Fact]
        public async Task ForAnAlreadyRequeuedDeadLetter_Returns409()
        {
            var deadLetterService = new FakeDeadLetterService();
            deadLetterService.Add("m1", status: DeadLetterStatus.Requeued);
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            var result = Assert.IsType<ProblemHttpResult>(await requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest("m1", null), CancellationToken.None));

            Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
            Assert.Contains("cannot be requeued", result.ProblemDetails.Detail);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task WithABlankMessageId_Returns400WithoutCallingTheService(string messageId)
        {
            var deadLetterService = new FakeDeadLetterService();
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            var result = Assert.IsType<ProblemHttpResult>(await requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest(messageId, null), CancellationToken.None));

            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
            Assert.Empty(deadLetterService.RequeueCalls);
        }

        [Fact]
        public async Task WhenTheBrokerFails_PropagatesTheExceptionAndLeavesTheDeadLetterPending()
        {
            var deadLetterService = new FakeDeadLetterService { RequeueFailure = new TimeoutException("broker unavailable") };
            var deadLetter = deadLetterService.Add("m1");
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            await Assert.ThrowsAsync<TimeoutException>(() => requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest("m1", null), CancellationToken.None));

            Assert.Equal(DeadLetterStatus.Pending, deadLetter.Status);
        }
    }
}
