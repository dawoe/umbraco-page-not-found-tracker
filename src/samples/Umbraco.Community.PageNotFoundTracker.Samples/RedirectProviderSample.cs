// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Community.PageNotFoundTracker.Core.Redirects;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Samples;

// docs:begin redirect-provider-class

/// <summary>
/// Sample redirect provider: the payload is <c>{ "targetUrl": "/somewhere" }</c> (what a custom form returns).
/// It must be singleton-compatible: the list endpoint asks CanHandle for every row.
/// </summary>
public sealed class RedirectProviderSample : IRedirectProvider
{
    private readonly ILogger<RedirectProviderSample> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedirectProviderSample"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public RedirectProviderSample(ILogger<RedirectProviderSample> logger) => this.logger = logger;

    /// <inheritdoc />
    public bool CanHandle(TrackedUrl url) => !url.IsFile;

    /// <inheritdoc />
    public Task<RedirectResult> CreateAsync(TrackedUrl url, JsonElement? payload, CancellationToken cancellationToken)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } body
            || !body.TryGetProperty("targetUrl", out var target)
            || target.GetString() is not { Length: > 0 } targetUrl
            || !targetUrl.StartsWith('/'))
        {
            // The message is shown to the editor inline in the dialog.
            return Task.FromResult(RedirectResult.Failure("Enter a target URL that starts with /."));
        }

        // Store the redirect in your own system here. The package removes the tracked URL when this succeeds.
        this.logger.LogInformation("Redirect {Domain}{Path} -> {Target}", url.Domain, url.RelativePath, targetUrl);
        return Task.FromResult(RedirectResult.Success());
    }
}

// docs:end redirect-provider-class
