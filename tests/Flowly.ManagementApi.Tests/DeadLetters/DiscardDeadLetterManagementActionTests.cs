using Flowly.DeadLetters;
using Flowly.ManagementApi.DeadLetters;
using Flowly.ManagementApi.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Flowly.ManagementApi.Tests.DeadLetters;

public class DiscardDeadLetterManagementActionTests
{
    public class Properties
    {
        [Fact]
        public void FeatureAreaIsDeadLetters()
        {
            var discardDeadLetterManagementAction = new DiscardDeadLetterManagementAction();

            Assert.Equal("dead-letters", discardDeadLetterManagementAction.FeatureArea);
        }

        [Fact]
        public void RequiresTheWriteCapability()
        {
            var discardDeadLetterManagementAction = new DiscardDeadLetterManagementAction();

            Assert.Equal(ManagementCapability.Write, discardDeadLetterManagementAction.RequiredCapability);
        }
    }

    public class Discard
    {
        [Fact]
        public async Task ForAPendingDeadLetter_Returns204AndDeletesIt()
        {
            var deadLetterService = new FakeDeadLetterService();
            deadLetterService.Add("m1");
            var discardDeadLetterManagementAction = new DiscardDeadLetterManagementAction();

            var result = await discardDeadLetterManagementAction.Discard(deadLetterService, new DiscardDeadLetterRequest("m1"), CancellationToken.None);

            Assert.IsType<NoContent>(result);
            Assert.Empty(await deadLetterService.GetDeadLetters());
        }

        [Fact]
        public async Task ForAnUnknownMessageId_Returns404WithAProblemBody()
        {
            var discardDeadLetterManagementAction = new DiscardDeadLetterManagementAction();

            var result = Assert.IsType<ProblemHttpResult>(await discardDeadLetterManagementAction.Discard(new FakeDeadLetterService(), new DiscardDeadLetterRequest("missing"), CancellationToken.None));

            Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
            Assert.Contains("missing", result.ProblemDetails.Detail);
        }

        [Fact]
        public async Task ForAnAlreadyRequeuedDeadLetter_Returns409AndKeepsIt()
        {
            var deadLetterService = new FakeDeadLetterService();
            deadLetterService.Add("m1", status: DeadLetterStatus.Requeued);
            var discardDeadLetterManagementAction = new DiscardDeadLetterManagementAction();

            var result = Assert.IsType<ProblemHttpResult>(await discardDeadLetterManagementAction.Discard(deadLetterService, new DiscardDeadLetterRequest("m1"), CancellationToken.None));

            Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
            Assert.Single(await deadLetterService.GetDeadLetters());
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task WithABlankMessageId_Returns400WithoutCallingTheService(string messageId)
        {
            var deadLetterService = new FakeDeadLetterService();
            var discardDeadLetterManagementAction = new DiscardDeadLetterManagementAction();

            var result = Assert.IsType<ProblemHttpResult>(await discardDeadLetterManagementAction.Discard(deadLetterService, new DiscardDeadLetterRequest(messageId), CancellationToken.None));

            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
            Assert.Empty(deadLetterService.DiscardCalls);
        }
    }
}
