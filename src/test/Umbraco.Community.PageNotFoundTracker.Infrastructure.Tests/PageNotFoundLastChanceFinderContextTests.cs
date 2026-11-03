// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Moq;
using Umbraco.Cms.Core.Routing;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the request context the <see cref="PageNotFoundLastChanceFinder"/> builds. Both branches must hand
/// over a decoded path (as Kestrel does: everything but <c>%2F</c>), because the normaliser does not decode.
/// </summary>
[TestFixture]
internal sealed class PageNotFoundLastChanceFinderContextTests
{
    /// <summary>
    /// Without an HTTP context the path of the published request's URI is decoded like Kestrel decodes it; the query
    /// string is left as it is.
    /// </summary>
    /// <param name="url">The request URL.</param>
    /// <param name="expected">The expected path and query.</param>
    [TestCase("https://x.com/caf%C3%A9", "/café")]
    [TestCase("https://x.com/a%2541", "/a%41")]
    [TestCase("https://x.com/a%2Fb", "/a%2Fb")]
    [TestCase("https://x.com/a%20b?q=%C3%A9", "/a b?q=%C3%A9")]
    [TestCase("https://x.com/", "/")]
    public void Without_HttpContext_Decodes_The_Path(string url, string expected)
    {
        var request = new Mock<IPublishedRequestBuilder>();
        request.Setup(r => r.Uri).Returns(new Uri(url));

        var context = PageNotFoundLastChanceFinder.CreateContext(request.Object, null);

        Assert.That(context.PathAndQuery, Is.EqualTo(expected));
    }

    /// <summary>
    /// With an HTTP context the path base and path (decoded by the server) are used as they are.
    /// </summary>
    [Test]
    public void With_HttpContext_Uses_The_Decoded_Request_Path()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.PathBase = "/base";
        httpContext.Request.Path = "/café";
        httpContext.Request.QueryString = new QueryString("?q=%C3%A9");
        httpContext.Request.Host = new HostString("x.com");

        var context = PageNotFoundLastChanceFinder.CreateContext(Mock.Of<IPublishedRequestBuilder>(), httpContext);

        Assert.That(context.PathAndQuery, Is.EqualTo("/base/café?q=%C3%A9"));
    }
}
