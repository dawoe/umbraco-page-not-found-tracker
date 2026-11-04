// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Community.PageNotFoundTracker.Core.Redirects;
using Umbraco.Community.PageNotFoundTracker.Core.Tracking;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.Redirects;

/// <summary>
/// Creates a redirect through the <see cref="IRedirectProvider"/> and deletes the tracked URL after a success.
/// </summary>
internal sealed class TrackedUrlRedirectService : ITrackedUrlRedirectService
{
    private const string NotSupportedMessage = "This tracked URL cannot be redirected by the active redirect provider.";
    private const string FailedMessage = "The redirect could not be created.";
    private const string NotRemovedMessage = "The redirect was created, but the tracked URL could not be removed from the list.";

    private readonly ITrackedUrlQueryService queryService;
    private readonly IRedirectProvider provider;
    private readonly ITrackedUrlRemover remover;
    private readonly ILogger<TrackedUrlRedirectService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackedUrlRedirectService"/> class.
    /// </summary>
    /// <param name="queryService">The tracked URL query service.</param>
    /// <param name="provider">The redirect provider.</param>
    /// <param name="remover">The tracked URL remover.</param>
    /// <param name="logger">The logger.</param>
    public TrackedUrlRedirectService(
        ITrackedUrlQueryService queryService,
        IRedirectProvider provider,
        ITrackedUrlRemover remover,
        ILogger<TrackedUrlRedirectService> logger)
    {
        this.queryService = queryService;
        this.provider = provider;
        this.remover = remover;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateRedirectOutcome> CreateAsync(int trackedUrlId, JsonElement? payload, CancellationToken cancellationToken)
    {
        TrackedUrl? url = await this.queryService.GetByIdAsync(trackedUrlId, cancellationToken);
        if (url is null)
        {
            return new CreateRedirectOutcome(CreateRedirectStatus.NotFound, null);
        }

        if (!this.provider.CanHandle(url))
        {
            return new CreateRedirectOutcome(CreateRedirectStatus.NotSupported, NotSupportedMessage);
        }

        RedirectResult result;
        try
        {
            result = await this.provider.CreateAsync(url, payload, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogError(ex, "The redirect provider failed to create a redirect for tracked URL {TrackedUrlId}.", trackedUrlId);
            return new CreateRedirectOutcome(CreateRedirectStatus.Failed, FailedMessage);
        }

        if (!result.Succeeded)
        {
            return new CreateRedirectOutcome(CreateRedirectStatus.Failed, result.Message ?? FailedMessage);
        }

        try
        {
            // False means the row was already gone, which is the state we want. No cancellation token: once the redirect exists,
            // a client abort must not skip the removal and leave the record without the "both facts" outcome and logging.
            await this.remover.DeleteAsync(url, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogError(ex, "The redirect for tracked URL {TrackedUrlId} was created, but the tracked URL could not be deleted.", trackedUrlId);
            return new CreateRedirectOutcome(CreateRedirectStatus.CreatedRecordNotRemoved, NotRemovedMessage);
        }

        return new CreateRedirectOutcome(CreateRedirectStatus.Created, null);
    }
}
