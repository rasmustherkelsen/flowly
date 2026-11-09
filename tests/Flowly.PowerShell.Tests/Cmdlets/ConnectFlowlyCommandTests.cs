using System.Management.Automation;
using System.Net;
using System.Reflection;
using System.Security;
using Flowly.PowerShell.Cmdlets;
using Flowly.PowerShell.Session;
using Flowly.PowerShell.Tests.Fakes;

namespace Flowly.PowerShell.Tests.Cmdlets;

public class ConnectFlowlyCommandTests
{
    public class Declaration
    {
        [Theory]
        [InlineData(nameof(ConnectFlowlyCommand.ApiKey))]
        [InlineData(nameof(ConnectFlowlyCommand.ClientSecret))]
        public void SecretParameterIsASecureString(string propertyName)
        {
            var property = typeof(ConnectFlowlyCommand).GetProperty(propertyName)!;

            Assert.Equal(typeof(SecureString), property.PropertyType);
        }

        [Fact]
        public void TenantIdBelongsOnlyToTheEntraParameterSet()
        {
            var parameterSets = ParameterSetsOf(nameof(ConnectFlowlyCommand.TenantId));

            Assert.Equal(["OAuth2"], parameterSets);
        }

        [Fact]
        public void AuthorityAndTokenEndpointBelongToDifferentParameterSets()
        {
            Assert.Empty(ParameterSetsOf(nameof(ConnectFlowlyCommand.Authority)).Intersect(ParameterSetsOf(nameof(ConnectFlowlyCommand.TokenEndpoint))));
        }

        private static string[] ParameterSetsOf(string propertyName)
            => typeof(ConnectFlowlyCommand).GetProperty(propertyName)!.GetCustomAttributes<ParameterAttribute>().Select(x => x.ParameterSetName).ToArray();
    }

    [Collection("FlowlySession")]
    public class ProcessRecord
    {
        [Fact]
        public void WithApiKeyAsSecureString_ConnectsWithoutOAuth2()
        {
            using var cmdletHost = new CmdletHost(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            cmdletHost.Invoke("Connect-Flowly", Parameters(("BaseUri", "https://example.test/flowly/management"), ("ApiKey", Secret("the-api-key"))));

            Assert.False(FlowlySession.Current!.UsesOAuth2);
        }

        [Fact]
        public void WithEmptyApiKey_Throws()
        {
            using var cmdletHost = new CmdletHost(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            var exception = Assert.Throws<CmdletInvocationException>(() =>
                cmdletHost.Invoke("Connect-Flowly", Parameters(("BaseUri", "https://example.test/flowly/management"), ("ApiKey", new SecureString()))));

            Assert.Contains("ApiKey must not be empty", exception.Message);
        }

        [Fact]
        public void WithTenantIdAndSecureClientSecret_ConnectsWithTheDefaultAuthority()
        {
            using var cmdletHost = new CmdletHost(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            cmdletHost.Invoke("Connect-Flowly", Parameters(
                ("BaseUri", "https://example.test/flowly/management"),
                ("TenantId", "the-tenant"),
                ("ClientId", "the-client"),
                ("ClientSecret", Secret("the-secret")),
                ("Scope", "api://flowly/.default")));

            var connection = FlowlySession.Current!;
            Assert.True(connection.UsesOAuth2);
            Assert.Equal("the-tenant", connection.TenantId);
            Assert.Equal(FlowlyConnection.DefaultAuthority, connection.Authority);
            Assert.Null(connection.TokenEndpoint);
            Assert.Equal("the-secret", connection.ClientSecret!.ToPlainText());
        }

        [Fact]
        public void WithAuthority_StoresIt()
        {
            using var cmdletHost = new CmdletHost(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            cmdletHost.Invoke("Connect-Flowly", Parameters(
                ("BaseUri", "https://example.test/flowly/management"),
                ("TenantId", "the-tenant"),
                ("ClientId", "the-client"),
                ("ClientSecret", Secret("the-secret")),
                ("Scope", "api://flowly/.default"),
                ("Authority", "https://login.microsoftonline.us")));

            Assert.Equal("https://login.microsoftonline.us", FlowlySession.Current!.Authority);
        }

        [Fact]
        public void WithAuthorityThatIsNotHttps_Throws()
        {
            using var cmdletHost = new CmdletHost(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            var exception = Assert.Throws<CmdletInvocationException>(() => cmdletHost.Invoke("Connect-Flowly", Parameters(
                ("BaseUri", "https://example.test/flowly/management"),
                ("TenantId", "the-tenant"),
                ("ClientId", "the-client"),
                ("ClientSecret", Secret("the-secret")),
                ("Scope", "api://flowly/.default"),
                ("Authority", "http://login.example.test"))));

            Assert.Contains("not an absolute https URL", exception.Message);
        }

        [Fact]
        public void WithTokenEndpointAndNoTenantId_ConnectsAndStoresTheTokenEndpoint()
        {
            using var cmdletHost = new CmdletHost(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            cmdletHost.Invoke("Connect-Flowly", Parameters(
                ("BaseUri", "https://example.test/flowly/management"),
                ("TokenEndpoint", "https://idp.example.test/connect/token"),
                ("ClientId", "the-client"),
                ("ClientSecret", Secret("the-secret")),
                ("Scope", "flowly")));

            var connection = FlowlySession.Current!;
            Assert.True(connection.UsesOAuth2);
            Assert.Null(connection.TenantId);
            Assert.Equal("https://idp.example.test/connect/token", connection.TokenEndpoint);
        }

        [Fact]
        public void WithTokenEndpointAndAuthority_Throws()
        {
            using var cmdletHost = new CmdletHost(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            Assert.ThrowsAny<RuntimeException>(() => cmdletHost.Invoke("Connect-Flowly", Parameters(
                ("BaseUri", "https://example.test/flowly/management"),
                ("TokenEndpoint", "https://idp.example.test/connect/token"),
                ("Authority", "https://login.microsoftonline.us"),
                ("ClientId", "the-client"),
                ("ClientSecret", Secret("the-secret")),
                ("Scope", "flowly"))));
        }

        [Fact]
        public void WithTokenEndpointAndTenantId_Throws()
        {
            using var cmdletHost = new CmdletHost(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            Assert.ThrowsAny<RuntimeException>(() => cmdletHost.Invoke("Connect-Flowly", Parameters(
                ("BaseUri", "https://example.test/flowly/management"),
                ("TokenEndpoint", "https://idp.example.test/connect/token"),
                ("TenantId", "the-tenant"),
                ("ClientId", "the-client"),
                ("ClientSecret", Secret("the-secret")),
                ("Scope", "flowly"))));
        }

        [Fact]
        public void WithTokenEndpointThatIsNotHttps_Throws()
        {
            using var cmdletHost = new CmdletHost(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            var exception = Assert.Throws<CmdletInvocationException>(() => cmdletHost.Invoke("Connect-Flowly", Parameters(
                ("BaseUri", "https://example.test/flowly/management"),
                ("TokenEndpoint", "http://idp.example.test/connect/token"),
                ("ClientId", "the-client"),
                ("ClientSecret", Secret("the-secret")),
                ("Scope", "flowly"))));

            Assert.Contains("not an absolute https URL", exception.Message);
        }

        [Fact]
        public void WithAPlainStringApiKey_Throws()
        {
            using var cmdletHost = new CmdletHost(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            Assert.ThrowsAny<RuntimeException>(() =>
                cmdletHost.Invoke("Connect-Flowly", Parameters(("BaseUri", "https://example.test/flowly/management"), ("ApiKey", "plain-text-key"))));
        }

        private static Dictionary<string, object?> Parameters(params (string Name, object? Value)[] parameters)
            => parameters.ToDictionary(x => x.Name, x => x.Value);

        private static SecureString Secret(string value) => new NetworkCredential(string.Empty, value).SecurePassword;
    }
}
