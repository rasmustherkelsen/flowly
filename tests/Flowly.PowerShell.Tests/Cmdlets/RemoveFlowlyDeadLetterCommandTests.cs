using System.Management.Automation;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Flowly.PowerShell.Cmdlets;
using Flowly.PowerShell.Tests.Fakes;

namespace Flowly.PowerShell.Tests.Cmdlets;

public class RemoveFlowlyDeadLetterCommandTests
{
    public class Declaration
    {
        [Fact]
        public void IsTheRemoveVerbWithTheFlowlyDeadLetterNoun()
        {
            var cmdlet = typeof(RemoveFlowlyDeadLetterCommand).GetCustomAttribute<CmdletAttribute>()!;

            Assert.Equal("Remove", cmdlet.VerbName);
            Assert.Equal("FlowlyDeadLetter", cmdlet.NounName);
        }

        [Fact]
        public void ConfirmsWithHighImpact()
        {
            var cmdlet = typeof(RemoveFlowlyDeadLetterCommand).GetCustomAttribute<CmdletAttribute>()!;

            Assert.True(cmdlet.SupportsShouldProcess);
            Assert.Equal(ConfirmImpact.High, cmdlet.ConfirmImpact);
        }
    }

    [Collection("FlowlySession")]
    public class ProcessRecord
    {
        [Fact]
        public void WithConfirmDisabled_PostsTheDiscardRequest()
        {
            var bodies = new List<JsonElement>();
            var handler = new RecordingHandler(request =>
            {
                bodies.Add(JsonDocument.Parse(request.Content!.ReadAsStream()).RootElement.Clone());

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });
            using var cmdletHost = new CmdletHost(handler);

            cmdletHost.Invoke("Remove-FlowlyDeadLetter", new Dictionary<string, object?> { ["MessageId"] = "m1", ["Confirm"] = false });

            Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
            Assert.Equal("/flowly/management/dead-letters/discard", handler.LastRequest.RequestUri!.AbsolutePath);
            Assert.Equal("m1", Assert.Single(bodies).GetProperty("messageId").GetString());
        }

        [Fact]
        public void WithWhatIf_DoesNotCallTheApi()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            using var cmdletHost = new CmdletHost(handler);

            cmdletHost.Invoke("Remove-FlowlyDeadLetter", new Dictionary<string, object?> { ["MessageId"] = "m1", ["WhatIf"] = true });

            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public void WhenTheDeadLetterIsUnknown_FailsWithTheProblemDetail()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"detail\":\"Dead letter with ID 'm1' was not found.\"}", System.Text.Encoding.UTF8, "application/problem+json")
            });
            using var cmdletHost = new CmdletHost(handler);

            var exception = Assert.Throws<CmdletInvocationException>(() => cmdletHost.Invoke(
                "Remove-FlowlyDeadLetter",
                new Dictionary<string, object?> { ["MessageId"] = "m1", ["Confirm"] = false }));

            Assert.Contains("was not found", exception.InnerException!.Message);
        }
    }
}
