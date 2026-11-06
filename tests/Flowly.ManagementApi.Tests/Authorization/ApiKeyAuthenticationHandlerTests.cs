using System.Text.Encodings.Web;
using Flowly.ManagementApi.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Flowly.ManagementApi.Tests.Authorization;

public class ApiKeyAuthenticationHandlerTests
{
    private const string ReadKey = "read-key";
    private const string WriteKey = "write-key";
    private const string ReadRole = "Flowly.Management.Read";
    private const string WriteRole = "Flowly.Management.Write";

    public class HandleAuthenticate
    {
        [Fact]
        public async Task MissingHeader_Fails()
        {
            var apiKeyAuthenticationHandler = await CreateHandler(ReadKey, WriteKey, null);

            var result = await apiKeyAuthenticationHandler.AuthenticateAsync();

            Assert.False(result.Succeeded);
        }

        [Fact]
        public async Task ReadKey_GrantsReadOnly()
        {
            var apiKeyAuthenticationHandler = await CreateHandler(ReadKey, WriteKey, ReadKey);

            var result = await apiKeyAuthenticationHandler.AuthenticateAsync();

            Assert.True(result.Succeeded);
            Assert.True(result.Principal!.IsInRole(ReadRole));
            Assert.False(result.Principal.IsInRole(WriteRole));
        }

        [Fact]
        public async Task WriteKey_GrantsReadAndWrite()
        {
            var apiKeyAuthenticationHandler = await CreateHandler(ReadKey, WriteKey, WriteKey);

            var result = await apiKeyAuthenticationHandler.AuthenticateAsync();

            Assert.True(result.Succeeded);
            Assert.True(result.Principal!.IsInRole(ReadRole));
            Assert.True(result.Principal.IsInRole(WriteRole));
        }

        [Fact]
        public async Task WrongKey_Fails()
        {
            var apiKeyAuthenticationHandler = await CreateHandler(ReadKey, WriteKey, "other-key");

            var result = await apiKeyAuthenticationHandler.AuthenticateAsync();

            Assert.False(result.Succeeded);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task BlankHeaderAgainstBlankConfiguredKeys_Fails(string headerValue)
        {
            var apiKeyAuthenticationHandler = await CreateHandler("", "", headerValue);

            var result = await apiKeyAuthenticationHandler.AuthenticateAsync();

            Assert.False(result.Succeeded);
        }

        [Fact]
        public async Task BlankHeaderAgainstConfiguredKeys_Fails()
        {
            var apiKeyAuthenticationHandler = await CreateHandler(ReadKey, WriteKey, "");

            var result = await apiKeyAuthenticationHandler.AuthenticateAsync();

            Assert.False(result.Succeeded);
        }

        [Fact]
        public async Task KeyDifferingOnlyInCase_Fails()
        {
            var apiKeyAuthenticationHandler = await CreateHandler(ReadKey, WriteKey, ReadKey.ToUpperInvariant());

            var result = await apiKeyAuthenticationHandler.AuthenticateAsync();

            Assert.False(result.Succeeded);
        }
    }

    private static async Task<ApiKeyAuthenticationHandler> CreateHandler(string readKey, string writeKey, string? headerValue)
    {
        var schemeOptions = new ApiKeyAuthenticationSchemeOptions { ReadApiKey = readKey, WriteApiKey = writeKey };
        var apiKeyAuthenticationHandler = new ApiKeyAuthenticationHandler(
            new FixedOptionsMonitor(schemeOptions),
            NullLoggerFactory.Instance,
            UrlEncoder.Default);
        var httpContext = new DefaultHttpContext();
        if (headerValue is not null)
            httpContext.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = headerValue;

        var scheme = new AuthenticationScheme(
            ApiKeyAuthenticationHandler.SchemeName,
            null,
            typeof(ApiKeyAuthenticationHandler));
        await apiKeyAuthenticationHandler.InitializeAsync(scheme, httpContext);
        return apiKeyAuthenticationHandler;
    }

    private sealed class FixedOptionsMonitor(ApiKeyAuthenticationSchemeOptions value)
        : IOptionsMonitor<ApiKeyAuthenticationSchemeOptions>
    {
        public ApiKeyAuthenticationSchemeOptions CurrentValue => value;

        public ApiKeyAuthenticationSchemeOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<ApiKeyAuthenticationSchemeOptions, string?> listener) => null;
    }
}
