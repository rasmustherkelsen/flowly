namespace Flowly.ManagementApi.Tests;

public class ManagementApiEndpointRouteBuilderExtensionsTests
{
    public class BuildDocsHtml
    {
        [Fact]
        public void ReferencesPinnedScriptWithIntegrityAndAnonymousCrossOrigin()
        {
            var html = ManagementApiEndpointRouteBuilderExtensions.BuildDocsHtml("/flowly/management");

            Assert.Contains(
                $"<script src=\"{ManagementApiEndpointRouteBuilderExtensions.DocsScriptUrl}\" integrity=\"{ManagementApiEndpointRouteBuilderExtensions.DocsScriptIntegrity}\" crossorigin=\"anonymous\"></script>",
                html);
        }

        [Fact]
        public void ScriptUrl_PinsExactVersion()
        {
            Assert.Matches(@"/@scalar/api-reference@\d+\.\d+\.\d+/", ManagementApiEndpointRouteBuilderExtensions.DocsScriptUrl);
        }

        [Fact]
        public void ScriptIntegrity_IsSha384()
        {
            Assert.StartsWith("sha384-", ManagementApiEndpointRouteBuilderExtensions.DocsScriptIntegrity);
        }

        [Fact]
        public void WithPlainBasePath_PointsSpecUrlAtOpenApiDocument()
        {
            var html = ManagementApiEndpointRouteBuilderExtensions.BuildDocsHtml("/flowly/management");

            Assert.Contains("data-url=\"/flowly/management/openapi.json\"", html);
        }

        [Fact]
        public void WithBasePathContainingMarkup_EncodesItInTheAttribute()
        {
            var html = ManagementApiEndpointRouteBuilderExtensions.BuildDocsHtml("/x\"></script><script>alert(1)</script>");

            Assert.DoesNotContain("<script>alert(1)", html);
            Assert.DoesNotContain("\"></script><script>", html);
            Assert.Contains("data-url=\"", html);
        }
    }
}
