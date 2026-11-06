using System.Management.Automation;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Flowly.PowerShell.Cmdlets;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Tests.Fakes;

namespace Flowly.PowerShell.Tests.Cmdlets;

public class ResumeFlowlyMessageStreamPartitionCommandTests
{
    public class Declaration
    {
        [Fact]
        public void IsTheResumeVerbWithTheFlowlyMessageStreamPartitionNoun()
        {
            var cmdlet = typeof(ResumeFlowlyMessageStreamPartitionCommand).GetCustomAttribute<CmdletAttribute>()!;

            Assert.Equal("Resume", cmdlet.VerbName);
            Assert.Equal("FlowlyMessageStreamPartition", cmdlet.NounName);
        }

        [Fact]
        public void ConfirmsWithHighImpact()
        {
            var cmdlet = typeof(ResumeFlowlyMessageStreamPartitionCommand).GetCustomAttribute<CmdletAttribute>()!;

            Assert.True(cmdlet.SupportsShouldProcess);
            Assert.Equal(ConfirmImpact.High, cmdlet.ConfirmImpact);
        }
    }

    [Collection("FlowlySession")]
    public class ProcessRecord
    {
        private static readonly DateTimeOffset ChangedAt = new(2026, 11, 1, 10, 30, 0, TimeSpan.Zero);

        [Fact]
        public void WithConfirmDisabled_PostsTheResumeRequest()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            using var cmdletHost = new CmdletHost(handler);

            cmdletHost.Invoke(
                "Resume-FlowlyMessageStreamPartition",
                new Dictionary<string, object?> { ["QueueName"] = "q", ["ConsumerName"] = "c", ["Partition"] = 2, ["Confirm"] = false });

            Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
            Assert.Equal("/flowly/management/message-streams/partitions/resume", handler.LastRequest.RequestUri!.AbsolutePath);
        }

        [Fact]
        public void WithWhatIf_DoesNotCallTheApi()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            using var cmdletHost = new CmdletHost(handler);

            cmdletHost.Invoke(
                "Resume-FlowlyMessageStreamPartition",
                new Dictionary<string, object?> { ["QueueName"] = "q", ["ConsumerName"] = "c", ["Partition"] = 2, ["WhatIf"] = true });

            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public void WhenThePartitionIsNotHalted_FailsWithTheProblemDetail()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("{\"detail\":\"partition 2 is Running, not Halted\"}", System.Text.Encoding.UTF8, "application/problem+json")
            });
            using var cmdletHost = new CmdletHost(handler);

            var exception = Assert.Throws<CmdletInvocationException>(() => cmdletHost.Invoke(
                "Resume-FlowlyMessageStreamPartition",
                new Dictionary<string, object?> { ["QueueName"] = "q", ["ConsumerName"] = "c", ["Partition"] = 2, ["Confirm"] = false }));

            Assert.Contains("partition 2 is Running, not Halted", exception.InnerException!.Message);
        }

        [Fact]
        public void ByPipeline_ResumesEachHaltedPartitionTheGetCmdletOutputs()
        {
            var resumed = new List<JsonElement>();
            var handler = new RecordingHandler(request => request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new[]
                    {
                        new StreamPartitionDto("MyHandler", "q", "c", 0, "Halted", ChangedAt, null),
                        new StreamPartitionDto("MyHandler", "q", "c", 3, "Halted", ChangedAt, null)
                    })
                }
                : RecordBody(request, resumed));
            using var cmdletHost = new CmdletHost(handler);

            cmdletHost.InvokePipeline(
                "Get-FlowlyMessageStreamPartition",
                "Resume-FlowlyMessageStreamPartition",
                new Dictionary<string, object?> { ["Confirm"] = false });

            Assert.Equal([0, 3], resumed.Select(body => body.GetProperty("partition").GetInt32()));
        }

        private static HttpResponseMessage RecordBody(HttpRequestMessage request, List<JsonElement> resumed)
        {
            resumed.Add(JsonDocument.Parse(request.Content!.ReadAsStream()).RootElement.Clone());

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
