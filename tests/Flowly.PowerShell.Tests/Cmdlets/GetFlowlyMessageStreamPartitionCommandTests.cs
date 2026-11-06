using System.Management.Automation;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Flowly.PowerShell.Cmdlets;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Tests.Fakes;

namespace Flowly.PowerShell.Tests.Cmdlets;

public class GetFlowlyMessageStreamPartitionCommandTests
{
    public class Declaration
    {
        [Fact]
        public void IsTheGetVerbWithTheFlowlyMessageStreamPartitionNoun()
        {
            var cmdlet = typeof(GetFlowlyMessageStreamPartitionCommand).GetCustomAttribute<CmdletAttribute>()!;

            Assert.Equal("Get", cmdlet.VerbName);
            Assert.Equal("FlowlyMessageStreamPartition", cmdlet.NounName);
        }

        [Fact]
        public void DoesNotAskForConfirmation()
        {
            var cmdlet = typeof(GetFlowlyMessageStreamPartitionCommand).GetCustomAttribute<CmdletAttribute>()!;

            Assert.False(cmdlet.SupportsShouldProcess);
        }
    }

    [Collection("FlowlySession")]
    public class ProcessRecord
    {
        private static readonly DateTimeOffset ChangedAt = new(2026, 11, 1, 10, 30, 0, TimeSpan.Zero);

        [Fact]
        public void WritesOneObjectPerPartition()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[]
                {
                    new StreamPartitionDto("MyHandler", "q", "c", 0, "Running", ChangedAt, null),
                    new StreamPartitionDto("MyHandler", "q", "c", 1, "Halted", ChangedAt, new StreamPartitionErrorDto("poison", "System.InvalidOperationException", ChangedAt))
                })
            });
            using var cmdletHost = new CmdletHost(handler);

            var output = cmdletHost.Invoke("Get-FlowlyMessageStreamPartition");

            Assert.Equal(["Running", "Halted"], output.Select(item => ((StreamPartitionDto)item.BaseObject).State));
        }

        [Fact]
        public void PassesTheFiltersToTheApi()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<StreamPartitionDto>()) });
            using var cmdletHost = new CmdletHost(handler);

            cmdletHost.Invoke(
                "Get-FlowlyMessageStreamPartition",
                new Dictionary<string, object?> { ["QueueName"] = "q", ["ConsumerName"] = "c", ["Partition"] = 2, ["State"] = "Halted" });

            var query = handler.LastRequest!.RequestUri!.Query;
            Assert.Contains("queue=q", query);
            Assert.Contains("consumer=c", query);
            Assert.Contains("partition=2", query);
            Assert.Contains("state=Halted", query);
        }

        [Fact]
        public void WithAnUnknownState_FailsParameterValidationWithoutCallingTheApi()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<StreamPartitionDto>()) });
            using var cmdletHost = new CmdletHost(handler);

            Assert.ThrowsAny<ParameterBindingException>(() =>
                cmdletHost.Invoke("Get-FlowlyMessageStreamPartition", new Dictionary<string, object?> { ["State"] = "Stuck" }));

            Assert.Equal(0, handler.RequestCount);
        }
    }
}
