// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Umbraco.Community.PageNotFoundTracker.Extensions;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Extensions;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="WebApplicationExtensions"/> class.
/// </summary>
[TestFixture]
internal sealed class WebApplicationExtensionsTests
{
    /// <summary>
    /// Tests that <see cref="WebApplicationExtensions.UsePageNotFoundTracker"/> can be called repeatedly without throwing.
    /// </summary>
    [Test]
    public void UsePageNotFoundTracker_Does_Not_Throw_And_Is_Idempotent()
    {
        var app = WebApplication.CreateBuilder().Build();

        Assert.DoesNotThrow(() =>
        {
            app.UsePageNotFoundTracker();
            app.UsePageNotFoundTracker();
        });
    }

    /// <summary>
    /// Tests that the file 404 middleware is added to the pipeline once, however often it is registered.
    /// </summary>
    [Test]
    public void UseFileNotFoundHandling_Adds_The_Middleware_Once()
    {
        var properties = new Dictionary<string, object?>();
        var app = new Mock<IApplicationBuilder>();
        app.SetupGet(x => x.Properties).Returns(properties);
        app.SetupGet(x => x.ApplicationServices).Returns(new ServiceCollection().BuildServiceProvider());
        app.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>())).Returns(app.Object);

        app.Object.UseFileNotFoundHandling();
        app.Object.UseFileNotFoundHandling();

        app.Verify(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()), Times.Once);
    }
}
