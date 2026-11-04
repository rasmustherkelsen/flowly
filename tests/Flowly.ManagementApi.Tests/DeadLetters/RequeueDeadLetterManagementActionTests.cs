using System.Security.Claims;
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

            var result = await requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest("m1", "ops"), new ClaimsPrincipal(), ManagementApiAuthMode.ApiKey, CancellationToken.None);

            Assert.IsType<NoContent>(result);
            Assert.Equal(("m1", "ops"), Assert.Single(deadLetterService.RequeueCalls));
            Assert.Equal(DeadLetterStatus.Requeued, deadLetter.Status);
        }

        [Fact]
        public async Task WithoutARequesterAndAnOAuth2Principal_RequeuesWithThePrincipalIdentity()
        {
            var deadLetterService = new FakeDeadLetterService();
            var deadLetter = deadLetterService.Add("m1");
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            await requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest("m1", null), AuthenticatedUser(new Claim(ClaimTypes.Name, "alice")), ManagementApiAuthMode.OAuth2, CancellationToken.None);

            Assert.Equal("alice", deadLetter.RequeuedBy);
        }

        [Fact]
        public async Task WithoutARequesterAndAPolicyPrincipal_RequeuesWithThePrincipalName()
        {
            var deadLetterService = new FakeDeadLetterService();
            var deadLetter = deadLetterService.Add("m1");
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            await requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest("m1", null), AuthenticatedUser(new Claim(ClaimTypes.Name, "bob")), ManagementApiAuthMode.ExistingPolicies, CancellationToken.None);

            Assert.Equal("bob", deadLetter.RequeuedBy);
        }

        [Fact]
        public async Task WithoutARequesterAndAnApiKeyPrincipal_RequeuesWithoutARequester()
        {
            var deadLetterService = new FakeDeadLetterService();
            var deadLetter = deadLetterService.Add("m1");
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            await requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest("m1", null), AuthenticatedUser(new Claim(ClaimTypes.Role, "Flowly.Management.Write")), ManagementApiAuthMode.ApiKey, CancellationToken.None);

            Assert.Null(deadLetter.RequeuedBy);
        }

        [Fact]
        public async Task WithoutARequesterAndAnUnauthenticatedPrincipal_RequeuesWithoutARequester()
        {
            var deadLetterService = new FakeDeadLetterService();
            var deadLetter = deadLetterService.Add("m1");
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            await requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest("m1", null), new ClaimsPrincipal(), ManagementApiAuthMode.OAuth2, CancellationToken.None);

            Assert.Null(deadLetter.RequeuedBy);
        }

        [Fact]
        public async Task WithAnExplicitRequester_StoresItInsteadOfThePrincipalIdentity()
        {
            var deadLetterService = new FakeDeadLetterService();
            var deadLetter = deadLetterService.Add("m1");
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            await requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest("m1", "ops"), AuthenticatedUser(new Claim(ClaimTypes.Name, "alice")), ManagementApiAuthMode.OAuth2, CancellationToken.None);

            Assert.Equal("ops", deadLetter.RequeuedBy);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task WithABlankRequester_StoresThePrincipalIdentity(string requeuedBy)
        {
            var deadLetterService = new FakeDeadLetterService();
            var deadLetter = deadLetterService.Add("m1");
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            await requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest("m1", requeuedBy), AuthenticatedUser(new Claim(ClaimTypes.Name, "alice")), ManagementApiAuthMode.OAuth2, CancellationToken.None);

            Assert.Equal("alice", deadLetter.RequeuedBy);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task WithABlankRequesterAndNoIdentity_StoresNoRequester(string requeuedBy)
        {
            var deadLetterService = new FakeDeadLetterService();
            var deadLetter = deadLetterService.Add("m1");
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            await requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest("m1", requeuedBy), new ClaimsPrincipal(), ManagementApiAuthMode.OAuth2, CancellationToken.None);

            Assert.Null(deadLetter.RequeuedBy);
        }

        [Fact]
        public async Task ForAnUnknownMessageId_Returns404WithAProblemBody()
        {
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            var result = Assert.IsType<ProblemHttpResult>(await requeueDeadLetterManagementAction.Requeue(new FakeDeadLetterService(), new RequeueDeadLetterRequest("missing", null), new ClaimsPrincipal(), ManagementApiAuthMode.ApiKey, CancellationToken.None));

            Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
            Assert.Contains("missing", result.ProblemDetails.Detail);
        }

        [Fact]
        public async Task ForAnAlreadyRequeuedDeadLetter_Returns409()
        {
            var deadLetterService = new FakeDeadLetterService();
            deadLetterService.Add("m1", status: DeadLetterStatus.Requeued);
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            var result = Assert.IsType<ProblemHttpResult>(await requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest("m1", null), new ClaimsPrincipal(), ManagementApiAuthMode.ApiKey, CancellationToken.None));

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

            var result = Assert.IsType<ProblemHttpResult>(await requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest(messageId, null), new ClaimsPrincipal(), ManagementApiAuthMode.ApiKey, CancellationToken.None));

            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
            Assert.Empty(deadLetterService.RequeueCalls);
        }

        [Fact]
        public async Task WhenTheBrokerFails_PropagatesTheExceptionAndLeavesTheDeadLetterPending()
        {
            var deadLetterService = new FakeDeadLetterService { RequeueFailure = new TimeoutException("broker unavailable") };
            var deadLetter = deadLetterService.Add("m1");
            var requeueDeadLetterManagementAction = new RequeueDeadLetterManagementAction();

            await Assert.ThrowsAsync<TimeoutException>(() => requeueDeadLetterManagementAction.Requeue(deadLetterService, new RequeueDeadLetterRequest("m1", null), new ClaimsPrincipal(), ManagementApiAuthMode.ApiKey, CancellationToken.None));

            Assert.Equal(DeadLetterStatus.Pending, deadLetter.Status);
        }
    }

    private static ClaimsPrincipal AuthenticatedUser(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));
}
