using System.Management.Automation;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Flowly.PowerShell.Cmdlets;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Tests.Fakes;

namespace Flowly.PowerShell.Tests.Cmdlets;

public class GetFlowlyDeadLetterCommandTests
{
    public class Declaration
    {
        [Fact]
        public void IsTheGetVerbWithTheFlowlyDeadLetterNoun()
        {
            var cmdlet = typeof(GetFlowlyDeadLetterCommand).GetCustomAttribute<CmdletAttribute>()!;

            Assert.Equal("Get", cmdlet.VerbName);
            Assert.Equal("FlowlyDeadLetter", cmdlet.NounName);
        }

        [Fact]
        public void DoesNotAskForConfirmation()
        {
            var cmdlet = typeof(GetFlowlyDeadLetterCommand).GetCustomAttribute<CmdletAttribute>()!;

            Assert.False(cmdlet.SupportsShouldProcess);
        }
    }

    [Collection("FlowlySession")]
    public class ProcessRecord
    {
        private static readonly DateTimeOffset DeadLetteredAt = new(2026, 11, 1, 10, 30, 0, TimeSpan.Zero);

        [Fact]
        public void WritesOneObjectPerDeadLetter()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[]
                {
                    new DeadLetterDto("m1", "q", null, "{}", "{}", DeadLetteredAt, null, null, "Pending", null, null),
                    new DeadLetterDto("m2", "q", null, "{}", "{}", DeadLetteredAt, null, null, "Requeued", DeadLetteredAt, "ops")
                })
            });
            using var cmdletHost = new CmdletHost(handler);

            var output = cmdletHost.Invoke("Get-FlowlyDeadLetter");

            Assert.Equal(["m1", "m2"], output.Select(item => ((DeadLetterDto)item.BaseObject).MessageId));
        }

        [Fact]
        public void OutputsTheReplacementBodyOfARequeuedDeadLetter()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[] { new DeadLetterDto("m1", "q", null, "{}", "{}", DeadLetteredAt, null, null, "Requeued", DeadLetteredAt, "ops", true, "{\"a\":1}") })
            });
            using var cmdletHost = new CmdletHost(handler);

            var deadLetter = (DeadLetterDto)Assert.Single(cmdletHost.Invoke("Get-FlowlyDeadLetter")).BaseObject;

            Assert.True(deadLetter.BodyReplaced);
            Assert.Equal("{\"a\":1}", deadLetter.ReplacementMessageBody);
            Assert.Equal("{}", deadLetter.MessageBody);
        }

        [Fact]
        public void PassesTheFiltersAndPagingToTheApi()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<DeadLetterDto>()) });
            using var cmdletHost = new CmdletHost(handler);

            cmdletHost.Invoke(
                "Get-FlowlyDeadLetter",
                new Dictionary<string, object?>
                {
                    ["QueueName"] = "q",
                    ["SubscriptionName"] = "billing",
                    ["Status"] = "Pending",
                    ["MessageId"] = "m1",
                    ["Skip"] = 5,
                    ["First"] = 10
                });

            var query = handler.LastRequest!.RequestUri!.Query;
            Assert.Contains("queue=q", query);
            Assert.Contains("subscription=billing", query);
            Assert.Contains("status=Pending", query);
            Assert.Contains("messageId=m1", query);
            Assert.Contains("skip=5", query);
            Assert.Contains("take=10", query);
        }

        [Fact]
        public void WithAnUnknownStatus_FailsParameterValidationWithoutCallingTheApi()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<DeadLetterDto>()) });
            using var cmdletHost = new CmdletHost(handler);

            Assert.ThrowsAny<ParameterBindingException>(() =>
                cmdletHost.Invoke("Get-FlowlyDeadLetter", new Dictionary<string, object?> { ["Status"] = "Discarded" }));

            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public void WithAFirstOutsideTheAllowedRange_FailsParameterValidationWithoutCallingTheApi()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<DeadLetterDto>()) });
            using var cmdletHost = new CmdletHost(handler);

            Assert.ThrowsAny<ParameterBindingException>(() =>
                cmdletHost.Invoke("Get-FlowlyDeadLetter", new Dictionary<string, object?> { ["First"] = 0 }));

            Assert.Equal(0, handler.RequestCount);
        }
    }
}
