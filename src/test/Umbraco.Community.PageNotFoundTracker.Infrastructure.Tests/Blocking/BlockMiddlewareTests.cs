// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Moq;
using Umbraco.Community.PageNotFoundTracker.Core.Blocking;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Blocking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Tests.Blocking;

/// <summary>
/// Unit tests for <see cref="BlockMiddleware"/>.
/// </summary>
[TestFixture]
internal sealed class BlockMiddlewareTests
{
    /// <summary>
    /// A blocked path gets the rule status and an empty body for any method, and next is not called.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <returns>A task.</returns>
    [TestCase("GET")]
    [TestCase("HEAD")]
    [TestCase("POST")]
    [TestCase("DELETE")]
    public async Task Blocked_path_answers_the_status_with_an_empty_body(string method)
    {
        var (status, length, bodyLength, nextCalled) = await Run("/wp-admin/*", 410, "/wp-admin/setup.php", method);

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(410));
            Assert.That(length, Is.Zero);
            Assert.That(bodyLength, Is.Zero);
            Assert.That(nextCalled, Is.False);
        });
    }

    /// <summary>
    /// The query string is ignored and matching is case-insensitive with slashes collapsed.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <param name="query">The query string.</param>
    /// <returns>A task.</returns>
    [TestCase("/old", "?x=1")]
    [TestCase("/OLD/", "")]
    [TestCase("//old", "")]
    public async Task Query_string_case_and_slashes_do_not_matter(string path, string query)
    {
        var (status, _, _, _) = await Run("/old", 403, path, "GET", query);

        Assert.That(status, Is.EqualTo(403));
    }

    /// <summary>
    /// Unmatched paths pass through.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Unmatched_path_passes_through()
    {
        var (status, _, _, nextCalled) = await Run("/old", 410, "/other", "GET");

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(200));
            Assert.That(nextCalled, Is.True);
        });
    }

    /// <summary>
    /// Umbraco-owned paths pass through and never ask the provider, even for a pattern the validator accepts.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns>A task.</returns>
    [TestCase("/umbraco/management/api/v1/server/status")]
    [TestCase("/UMBRACO")]
    [TestCase("/App_Plugins/x/entry-point.js")]
    [TestCase("/_framework/blazor.js")]
    public async Task Owned_path_passes_through(string path)
    {
        foreach (string pattern in new[] { "/*", "/*/management/*", "/umbraco/*", "/*/status" })
        {
            var (status, _, _, nextCalled) = await Run(pattern, 410, path, "GET");

            Assert.Multiple(() =>
            {
                Assert.That(status, Is.EqualTo(200), pattern);
                Assert.That(nextCalled, Is.True, pattern);
            });
        }
    }

    /// <summary>
    /// A path that only starts like an owned prefix is not owned and can be blocked.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Umbracoextra_is_not_owned_and_is_blockable()
    {
        var (status, _, _, _) = await Run("/umbracoextra", 410, "/umbracoextra", "GET");

        Assert.That(status, Is.EqualTo(410));
    }

    /// <summary>
    /// A malformed percent escape in the path does not throw and is matched literally (the path is not decoded again).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Malformed_percent_escape_does_not_throw()
    {
        var (status, _, _, nextCalled) = await Run("/old*", 410, "/old%zz%", "GET");

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(410));
            Assert.That(nextCalled, Is.False);
        });
    }

    /// <summary>
    /// The middleware waits for the provider's snapshot.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Waits_for_the_provider()
    {
        var gate = new TaskCompletionSource<BlockSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new Mock<IBlockSnapshotProvider>();
        provider.Setup(p => p.GetAsync(It.IsAny<CancellationToken>())).Returns(() => new ValueTask<BlockSnapshot>(gate.Task));
        var nextCalled = false;
        var middleware = new BlockMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = Context("/old", "GET", string.Empty);

        Task invoke = middleware.InvokeAsync(context, provider.Object);
        await Task.Delay(50);
        Assert.That(invoke.IsCompleted, Is.False);

        gate.SetResult(Snapshot("/old", 410));
        await invoke.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(410));
            Assert.That(nextCalled, Is.False);
        });
    }

    private static BlockSnapshot Snapshot(string pattern, int status) =>
        BlockSnapshot.Build([new BlockRule { Pattern = BlockPatternNormalizer.Normalize(pattern), StatusCode = status }], []);

    private static DefaultHttpContext Context(string path, string method, string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = new PathString(path);
        context.Request.QueryString = new QueryString(query);
        context.Request.Host = new HostString("localhost", 44401);
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<(int Status, long? Length, long BodyLength, bool NextCalled)> Run(
        string pattern, int ruleStatus, string path, string method, string query = "")
    {
        var provider = new Mock<IBlockSnapshotProvider>();
        provider.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<BlockSnapshot>(Snapshot(pattern, ruleStatus)));
        var nextCalled = false;
        var middleware = new BlockMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = Context(path, method, query);

        await middleware.InvokeAsync(context, provider.Object);

        return (context.Response.StatusCode, context.Response.ContentLength, context.Response.Body.Length, nextCalled);
    }
}
