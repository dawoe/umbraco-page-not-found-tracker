// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

/// <summary>
/// Umbraco's last-chance content finder, replaced by the package so the 404 page is resolved by
/// <see cref="INotFoundPageResolver"/> before Umbraco's own <see cref="ContentFinderByConfigured404"/> runs.
/// </summary>
/// <remarks>
/// <para>
/// Umbraco calls the last-chance finder only for front-end requests that its routing could not match to a published
/// page; backoffice, Management API and other reserved paths, and paths with a file extension never get here.
/// </para>
/// <para>
/// When the resolver returns a page, it becomes the published content of the request, with the page's culture (when
/// it has one) and status 404. Umbraco then picks the page's template and renders it as a normal page, so the visitor's
/// URL stays the original one. When the resolver returns nothing or throws, Umbraco's
/// <see cref="ContentFinderByConfigured404"/> runs, so the default behaviour is unchanged. A cancellation because the
/// visitor aborted the request is logged at debug level only and also falls back to Umbraco's finder.
/// </para>
/// <para>
/// When the resolved 404 page is under public-access protection, Umbraco's route value transformer rewrites the request
/// to the protection's login or no-access page with a new published request, which drops the 404 status, and no
/// notification fires afterwards. The finder therefore also registers a <see cref="HttpResponse.OnStarting(Func{object, Task}, object)"/>
/// callback (once per request) that turns a 200 into a 404, so the response is a 404 as on the file path. Redirects
/// and challenges are left alone.
/// </para>
/// <para>
/// Every request that ends here is reported once to <see cref="INotFoundRecorder"/>, even when Umbraco calls the
/// finder a second time for the same request (it does so when the found page has no template).
/// </para>
/// </remarks>
internal sealed class PageNotFoundLastChanceFinder : IContentLastChanceFinder
{
    private readonly INotFoundPageResolver resolver;
    private readonly INotFoundRecorder recorder;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<PageNotFoundLastChanceFinder> logger;
    private readonly IContentLastChanceFinder umbracoFinder;

    /// <summary>
    /// Initializes a new instance of the <see cref="PageNotFoundLastChanceFinder"/> class.
    /// </summary>
    /// <param name="resolver">The 404 page resolver.</param>
    /// <param name="recorder">The recorder for 404 occurrences.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The service provider, used to create Umbraco's own last-chance finder.</param>
    public PageNotFoundLastChanceFinder(
        INotFoundPageResolver resolver,
        INotFoundRecorder recorder,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PageNotFoundLastChanceFinder> logger,
        IServiceProvider serviceProvider)
    {
        this.resolver = resolver;
        this.recorder = recorder;
        this.httpContextAccessor = httpContextAccessor;
        this.logger = logger;

        // The package replaces the IContentLastChanceFinder registration, so Umbraco's finder is created here.
        this.umbracoFinder = ActivatorUtilities.CreateInstance<ContentFinderByConfigured404>(serviceProvider);
    }

    /// <inheritdoc />
    public async Task<bool> TryFindContent(IPublishedRequestBuilder request)
    {
        var httpContext = this.httpContextAccessor.HttpContext;
        var cancellationToken = httpContext?.RequestAborted ?? CancellationToken.None;
        var context = CreateContext(request, httpContext);
        Domain? domain = request.Domain;

        var page = await this.ResolveAsync(context, domain, cancellationToken);

        bool found;
        if (page is not null)
        {
            request.SetPublishedContent(page.Content);
            if (!string.IsNullOrWhiteSpace(page.Culture))
            {
                request.SetCulture(page.Culture);
            }

            request.SetIs404();
            KeepNotFoundStatus(httpContext);
            found = true;
        }
        else
        {
            found = await this.umbracoFinder.TryFindContent(request);
        }

        await this.RecordAsync(httpContext, context, domain, page is not null, cancellationToken);

        return found;
    }

    /// <summary>
    /// Registers, once per request, a callback that turns a 200 into a 404 when the response starts. Umbraco's
    /// public-access rewrite replaces the published request of a protected 404 page with one for the login or no-access
    /// page and drops the 404 status; this keeps the response a 404. Other statuses (redirects, challenges) are left
    /// alone, and the callback does nothing when the status is already 404.
    /// </summary>
    private static void KeepNotFoundStatus(HttpContext? httpContext)
    {
        if (httpContext is null || httpContext.Items.ContainsKey(NotFoundRequestItems.StatusGuardKey))
        {
            return;
        }

        httpContext.Items[NotFoundRequestItems.StatusGuardKey] = true;
        httpContext.Response.OnStarting(
            static state =>
            {
                var response = ((HttpContext)state).Response;
                if (response.StatusCode == StatusCodes.Status200OK)
                {
                    response.StatusCode = StatusCodes.Status404NotFound;
                }

                return Task.CompletedTask;
            },
            httpContext);
    }

    private static NotFoundRequestContext CreateContext(IPublishedRequestBuilder request, HttpContext? httpContext)
    {
        if (httpContext is not null)
        {
            var httpRequest = httpContext.Request;
            var path = httpRequest.PathBase.Add(httpRequest.Path).Value ?? "/";
            return new NotFoundRequestContext(
                path + httpRequest.QueryString.Value,
                httpRequest.Host.Host,
                httpRequest.Host.Port,
                UmbracoOwnedPaths.IsFileRequest(path));
        }

        var uri = request.Uri;
        return new NotFoundRequestContext(
            uri.PathAndQuery,
            uri.Host,
            uri.IsDefaultPort ? null : uri.Port,
            UmbracoOwnedPaths.IsFileRequest(uri.AbsolutePath));
    }

    private async Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken cancellationToken)
    {
        try
        {
            return await this.resolver.ResolveAsync(context, domain, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            this.logger.LogDebug("The request for {PathAndQuery} was aborted while its 404 page was resolved; Umbraco's default 404 handling applies.", context.PathAndQuery);
            return null;
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "Resolving the 404 page for {PathAndQuery} failed; Umbraco's default 404 handling applies.", context.PathAndQuery);
            return null;
        }
    }

    private async Task RecordAsync(HttpContext? httpContext, NotFoundRequestContext context, Domain? domain, bool pageResolved, CancellationToken cancellationToken)
    {
        if (httpContext is not null && !NotFoundRequestItems.TryMarkRecorded(httpContext))
        {
            return;
        }

        var culture = string.IsNullOrWhiteSpace(domain?.Culture) ? null : domain.Culture;
        var occurrence = new NotFoundOccurrence(context.PathAndQuery, domain, culture, context.IsFile, pageResolved);

        try
        {
            await this.recorder.RecordAsync(occurrence, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            this.logger.LogDebug("The request for {PathAndQuery} was aborted while its 404 was recorded.", context.PathAndQuery);
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "Recording the 404 for {PathAndQuery} failed.", context.PathAndQuery);
        }
    }
}
