using System.Management.Automation;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Flowly.PowerShell.Cmdlets;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Tests.Fakes;

namespace Flowly.PowerShell.Tests.Cmdlets;

public class SubmitFlowlyDeadLetterCommandTests
{
    public class Declaration
    {
        [Fact]
        public void IsTheSubmitVerbWithTheFlowlyDeadLetterNoun()
        {
            var cmdlet = typeof(SubmitFlowlyDeadLetterCommand).GetCustomAttribute<CmdletAttribute>()!;

            Assert.Equal("Submit", cmdlet.VerbName);
            Assert.Equal("FlowlyDeadLetter", cmdlet.NounName);
        }

        [Fact]
        public void ConfirmsWithHighImpact()
        {
            var cmdlet = typeof(SubmitFlowlyDeadLetterCommand).GetCustomAttribute<CmdletAttribute>()!;

            Assert.True(cmdlet.SupportsShouldProcess);
            Assert.Equal(ConfirmImpact.High, cmdlet.ConfirmImpact);
        }
    }

    [Collection("FlowlySession")]
    public class ProcessRecord
    {
        private static readonly DateTimeOffset DeadLetteredAt = new(2026, 11, 1, 10, 30, 0, TimeSpan.Zero);

        [Fact]
        public void WithConfirmDisabled_PostsTheRequeueRequest()
        {
            var requests = new List<JsonElement>();
            var handler = new RecordingHandler(request => RecordBody(request, requests));
            using var cmdletHost = new CmdletHost(handler);

            cmdletHost.Invoke(
                "Submit-FlowlyDeadLetter",
                new Dictionary<string, object?> { ["MessageId"] = "m1", ["RequeuedBy"] = "ops", ["Confirm"] = false });

            Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
            Assert.Equal("/flowly/management/dead-letters/requeue", handler.LastRequest.RequestUri!.AbsolutePath);
            Assert.Equal("m1", Assert.Single(requests).GetProperty("messageId").GetString());
            Assert.Equal("ops", requests[0].GetProperty("requeuedBy").GetString());
        }

        [Fact]
        public void WithWhatIf_DoesNotCallTheApi()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            using var cmdletHost = new CmdletHost(handler);

            cmdletHost.Invoke("Submit-FlowlyDeadLetter", new Dictionary<string, object?> { ["MessageId"] = "m1", ["WhatIf"] = true });

            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public void WhenTheDeadLetterIsAlreadyRequeued_FailsWithTheProblemDetail()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("{\"detail\":\"Dead letter 'm1' has status 'Requeued' and cannot be requeued.\"}", System.Text.Encoding.UTF8, "application/problem+json")
            });
            using var cmdletHost = new CmdletHost(handler);

            var exception = Assert.Throws<CmdletInvocationException>(() => cmdletHost.Invoke(
                "Submit-FlowlyDeadLetter",
                new Dictionary<string, object?> { ["MessageId"] = "m1", ["Confirm"] = false }));

            Assert.Contains("cannot be requeued", exception.InnerException!.Message);
        }

        [Fact]
        public void ByPipeline_RequeuesEachDeadLetterTheGetCmdletOutputs()
        {
            var requeued = new List<JsonElement>();
            var handler = new RecordingHandler(request => request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new[]
                    {
                        new DeadLetterDto("m1", "q", null, "{}", "{}", DeadLetteredAt, null, null, "Pending", null, null),
                        new DeadLetterDto("m2", "q", null, "{}", "{}", DeadLetteredAt, null, null, "Pending", null, null)
                    })
                }
                : RecordBody(request, requeued));
            using var cmdletHost = new CmdletHost(handler);

            cmdletHost.InvokePipeline(
                "Get-FlowlyDeadLetter",
                "Submit-FlowlyDeadLetter",
                new Dictionary<string, object?> { ["Confirm"] = false });

            Assert.Equal(["m1", "m2"], requeued.Select(body => body.GetProperty("messageId").GetString()));
        }

        private static HttpResponseMessage RecordBody(HttpRequestMessage request, List<JsonElement> requests)
        {
            requests.Add(JsonDocument.Parse(request.Content!.ReadAsStream()).RootElement.Clone());

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
