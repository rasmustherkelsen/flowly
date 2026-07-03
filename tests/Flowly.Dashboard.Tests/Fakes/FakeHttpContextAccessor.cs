using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Dashboard.Tests.Fakes;

internal sealed class FakeHttpContextAccessor : IHttpContextAccessor
{
    public FakeHttpContextAccessor(FakeAuthorizationService? authorizationService)
    {
        if (authorizationService is null)
        {
            HttpContext = null;
            return;
        }

        var services = new ServiceCollection();
        services.AddSingleton<IAuthorizationService>(authorizationService);

        HttpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
    }

    public HttpContext? HttpContext { get; set; }
}
