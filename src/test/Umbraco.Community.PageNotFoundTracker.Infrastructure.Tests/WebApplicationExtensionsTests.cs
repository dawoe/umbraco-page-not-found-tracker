// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Umbraco.Community.PageNotFoundTracker.Extensions;

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
}
