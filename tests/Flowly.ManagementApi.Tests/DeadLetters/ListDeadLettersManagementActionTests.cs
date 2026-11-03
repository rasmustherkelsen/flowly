using Flowly.DeadLetters;
using Flowly.ManagementApi.DeadLetters;
using Flowly.ManagementApi.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Flowly.ManagementApi.Tests.DeadLetters;

public class ListDeadLettersManagementActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 30, 0, TimeSpan.Zero);

    public class Properties
    {
        [Fact]
        public void FeatureAreaIsDeadLetters()
        {
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            Assert.Equal("dead-letters", listDeadLettersManagementAction.FeatureArea);
        }

        [Fact]
        public void RequiresTheReadCapability()
        {
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            Assert.Equal(ManagementCapability.Read, listDeadLettersManagementAction.RequiredCapability);
        }
    }

    public class List
    {
        [Fact]
        public async Task WithoutDeadLetters_ReturnsAnEmptyList()
        {
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            var result = Assert.IsType<Ok<List<DeadLetterResponse>>>(await listDeadLettersManagementAction.List(new FakeDeadLetterService(), null, null, null, null, null, null, CancellationToken.None));

            Assert.Empty(result.Value!);
        }

        [Fact]
        public async Task ReturnsNewestFirstWithTheMessageIdAsTieBreaker()
        {
            var deadLetterService = new FakeDeadLetterService();
            deadLetterService.Add("b", deadLetteredAt: Now);
            deadLetterService.Add("c", deadLetteredAt: Now.AddMinutes(5));
            deadLetterService.Add("a", deadLetteredAt: Now);
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            var result = Assert.IsType<Ok<List<DeadLetterResponse>>>(await listDeadLettersManagementAction.List(deadLetterService, null, null, null, null, null, null, CancellationToken.None));

            Assert.Equal(["c", "a", "b"], result.Value!.Select(deadLetter => deadLetter.MessageId));
        }

        [Fact]
        public async Task MapsEveryField()
        {
            var deadLetterService = new FakeDeadLetterService();
            var requeued = deadLetterService.Add("m1", "orders-topic", "billing", DeadLetterStatus.Requeued, Now, "MaxDeliveryCountExceeded");
            requeued.RequeuedAt = Now.AddHours(1);
            requeued.RequeuedBy = "ops";
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            var result = Assert.IsType<Ok<List<DeadLetterResponse>>>(await listDeadLettersManagementAction.List(deadLetterService, null, null, null, null, null, null, CancellationToken.None));

            var deadLetter = Assert.Single(result.Value!);
            Assert.Equal(
                new DeadLetterResponse("m1", "orders-topic", "billing", "{}", "{}", Now, "MaxDeliveryCountExceeded", null, "Requeued", Now.AddHours(1), "ops"),
                deadLetter);
        }

        [Fact]
        public async Task FiltersByQueue()
        {
            var deadLetterService = new FakeDeadLetterService();
            deadLetterService.Add("a", "orders");
            deadLetterService.Add("b", "invoices");
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            var result = Assert.IsType<Ok<List<DeadLetterResponse>>>(await listDeadLettersManagementAction.List(deadLetterService, "invoices", null, null, null, null, null, CancellationToken.None));

            Assert.Equal("b", Assert.Single(result.Value!).MessageId);
        }

        [Fact]
        public async Task FiltersBySubscription()
        {
            var deadLetterService = new FakeDeadLetterService();
            deadLetterService.Add("a", "topic", "billing");
            deadLetterService.Add("b", "topic", "shipping");
            deadLetterService.Add("c", "orders");
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            var result = Assert.IsType<Ok<List<DeadLetterResponse>>>(await listDeadLettersManagementAction.List(deadLetterService, null, "shipping", null, null, null, null, CancellationToken.None));

            Assert.Equal("b", Assert.Single(result.Value!).MessageId);
        }

        [Fact]
        public async Task FiltersByStatusIgnoringCase()
        {
            var deadLetterService = new FakeDeadLetterService();
            deadLetterService.Add("a");
            deadLetterService.Add("b", status: DeadLetterStatus.Requeued);
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            var result = Assert.IsType<Ok<List<DeadLetterResponse>>>(await listDeadLettersManagementAction.List(deadLetterService, null, null, "requeued", null, null, null, CancellationToken.None));

            Assert.Equal("b", Assert.Single(result.Value!).MessageId);
        }

        [Fact]
        public async Task FiltersByMessageId()
        {
            var deadLetterService = new FakeDeadLetterService();
            deadLetterService.Add("a");
            deadLetterService.Add("b");
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            var result = Assert.IsType<Ok<List<DeadLetterResponse>>>(await listDeadLettersManagementAction.List(deadLetterService, null, null, null, "a", null, null, CancellationToken.None));

            Assert.Equal("a", Assert.Single(result.Value!).MessageId);
        }

        [Fact]
        public async Task CombinesFilters()
        {
            var deadLetterService = new FakeDeadLetterService();
            deadLetterService.Add("a", "orders");
            deadLetterService.Add("b", "orders", status: DeadLetterStatus.Requeued);
            deadLetterService.Add("c", "invoices");
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            var result = Assert.IsType<Ok<List<DeadLetterResponse>>>(await listDeadLettersManagementAction.List(deadLetterService, "orders", null, "Pending", null, null, null, CancellationToken.None));

            Assert.Equal("a", Assert.Single(result.Value!).MessageId);
        }

        [Fact]
        public async Task PagesWithSkipAndTake()
        {
            var deadLetterService = new FakeDeadLetterService();
            deadLetterService.Add("a", deadLetteredAt: Now.AddMinutes(4));
            deadLetterService.Add("b", deadLetteredAt: Now.AddMinutes(3));
            deadLetterService.Add("c", deadLetteredAt: Now.AddMinutes(2));
            deadLetterService.Add("d", deadLetteredAt: Now.AddMinutes(1));
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            var result = Assert.IsType<Ok<List<DeadLetterResponse>>>(await listDeadLettersManagementAction.List(deadLetterService, null, null, null, null, 1, 2, CancellationToken.None));

            Assert.Equal(["b", "c"], result.Value!.Select(deadLetter => deadLetter.MessageId));
        }

        [Fact]
        public async Task WithSkipAndWithoutTake_ReturnsTheRest()
        {
            var deadLetterService = new FakeDeadLetterService();
            deadLetterService.Add("a", deadLetteredAt: Now.AddMinutes(2));
            deadLetterService.Add("b", deadLetteredAt: Now.AddMinutes(1));
            deadLetterService.Add("c", deadLetteredAt: Now);
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            var result = Assert.IsType<Ok<List<DeadLetterResponse>>>(await listDeadLettersManagementAction.List(deadLetterService, null, null, null, null, 1, null, CancellationToken.None));

            Assert.Equal(["b", "c"], result.Value!.Select(deadLetter => deadLetter.MessageId));
        }

        [Fact]
        public async Task WithAnUnknownStatus_Returns400()
        {
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            var result = Assert.IsType<ProblemHttpResult>(await listDeadLettersManagementAction.List(new FakeDeadLetterService(), null, null, "Discarded", null, null, null, CancellationToken.None));

            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Fact]
        public async Task WithANegativeSkip_Returns400()
        {
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            var result = Assert.IsType<ProblemHttpResult>(await listDeadLettersManagementAction.List(new FakeDeadLetterService(), null, null, null, null, -1, null, CancellationToken.None));

            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(ListDeadLettersManagementAction.MaxPageSize + 1)]
        public async Task WithATakeOutsideTheAllowedRange_Returns400(int take)
        {
            var listDeadLettersManagementAction = new ListDeadLettersManagementAction();

            var result = Assert.IsType<ProblemHttpResult>(await listDeadLettersManagementAction.List(new FakeDeadLetterService(), null, null, null, null, null, take, CancellationToken.None));

            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        }
    }
}
