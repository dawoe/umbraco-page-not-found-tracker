// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Routing;
using Umbraco.Cms.Web.Website.Routing;
using Umbraco.Community.PageNotFoundTracker.Core.NotFound;

namespace Umbraco.Community.PageNotFoundTracker.Infrastructure.NotFound;

/// <summary>
/// Answers a missing file with the 404 page: when the rest of the pipeline ended a GET or HEAD for a file in a 404
/// without a body, the page from <see cref="INotFoundPageResolver"/> is rendered by Umbraco with status 404.
/// </summary>
/// <remarks>
/// <para>
/// <b>Which requests.</b> The middleware runs <c>next</c> first and acts only afterwards, when all of these hold: the
/// method is GET or HEAD; the last path segment has an extension (<see cref="UmbracoOwnedPaths.IsFileRequest"/>, so
/// <c>/media</c> is covered too); the path is not Umbraco-owned (<see cref="UmbracoOwnedPaths.IsUmbracoOwned"/>) and not
/// below <c>/.well-known</c>; the status is 404; the response has not started and has no content type or length; and
/// the request was neither recorded by the page path (<see cref="PageNotFoundLastChanceFinder"/>) nor handled here
/// before. The keys in <see cref="NotFoundRequestItems"/> guarantee that a request is recorded and rendered once in
/// total. Umbraco never routes a path with an extension to its content finders, so in practice the two code paths never
/// see the same request.
/// </para>
/// <para>
/// <b>Resolving and the signal.</b> The domain comes from <see cref="IRequestDomainMatcher"/> and is passed to the
/// resolver. Every handled request is reported once to <see cref="INotFoundRecorder"/>, with <c>IsFile</c> true. When
/// the resolver returns nothing or throws, the original 404 passes through unchanged. Exceptions of the resolver, the
/// recorder and the rendering are logged and never turn the response into a 500; a cancellation because the visitor
/// aborted the request is not logged as an error.
/// </para>
/// <para>
/// <b>Render mechanism (spike result).</b> Umbraco renders a page only through endpoint routing
/// (<c>UmbracoRouteValueTransformer</c>), which ignores paths with an extension, and <c>UmbracoRequestMiddleware</c>
/// creates no Umbraco context for them. The middleware therefore builds the published request itself, the same way
/// Umbraco's <c>UmbracoVirtualPageRoute.CreatePublishedRequest</c> does for virtual pages, and then executes the render
/// controller through MVC, the same way an MVC endpoint does:
/// </para>
/// <list type="number">
/// <item><description><see cref="IUmbracoContextFactory.EnsureUmbracoContext"/> (disposed afterwards), so the Umbraco
/// context exists for the file request and its URL is the visitor's original URL.</description></item>
/// <item><description><see cref="IPublishedRouter.CreateRequestAsync"/> for the original URL; the matched domain
/// (<see cref="IPublishedRequestBuilder.SetDomain"/>), the 404 page as published content, the page's culture
/// (<see cref="NotFoundPage.Culture"/>, set after the domain so it wins) and <c>SetIs404()</c>.
/// Without a domain Umbraco's own domain lookup sets the culture, which is its default language, the same fallback the
/// default resolver uses.</description></item>
/// <item><description><see cref="IPublishedRouter.RouteRequestAsync"/>: the content is preset, so the content finders
/// are skipped, but Umbraco still picks the template, handles wildcard domains, sets the variation context and raises
/// its routing notification.</description></item>
/// <item><description><see cref="IUmbracoRouteValuesFactory"/> creates the route values (template and hijacked controller
/// lookup, as for any page); they are set as the <see cref="UmbracoRouteValues"/> feature and as the Umbraco context's
/// published request.</description></item>
/// <item><description><see cref="IActionInvokerFactory"/> executes the controller action with those route values. The
/// action, its filters (including Umbraco's filter that applies the 404 status) and the view result run as in any MVC
/// request, so the template, partials, navigation and view components render with the 404 page as the current
/// page.</description></item>
/// </list>
/// <para>
/// The request path is never rewritten and nothing is redirected, so the visitor's URL stays the original one. The
/// pipeline is not re-executed, so a 404 page that fails to render cannot loop; the handled key also stops a second
/// pass. A HEAD request renders with the body discarded, so its headers match the GET. Re-executing the pipeline with
/// the 404 page's URL (as <c>UseStatusCodePagesWithReExecute</c> does) was rejected: Umbraco would route the 404 page's
/// own URL (status 200, culture and domain from that URL, the Umbraco context URL no longer the original), and the
/// 404 page needs a routable URL at all. A pre-rendered string was not an option (spec 2). Not run for the file path:
/// Umbraco's begin/end request notifications and public access protection of the 404 page.
/// </para>
/// </remarks>
internal sealed class FileNotFoundMiddleware
{
    private const string WellKnownPrefix = "/.well-known";

    private readonly RequestDelegate next;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileNotFoundMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware.</param>
    public FileNotFoundMiddleware(RequestDelegate next) => this.next = next;

    /// <summary>
    /// Runs the rest of the pipeline and answers a bodyless 404 for a missing file with the 404 page.
    /// </summary>
    /// <param name="httpContext">The HTTP context of the request.</param>
    /// <returns>A task that completes when the response is done.</returns>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        await this.next(httpContext);

        if (!IsUnhandledFileNotFound(httpContext))
        {
            return;
        }

        httpContext.Items[NotFoundRequestItems.FileHandledKey] = true;

        var logger = httpContext.RequestServices.GetRequiredService<ILogger<FileNotFoundMiddleware>>();
        try
        {
            await HandleAsync(httpContext, logger);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("The request for {Path} was aborted while its 404 was handled.", httpContext.Request.Path);
        }
    }

    private static bool IsUnhandledFileNotFound(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;
        var path = request.Path.Value ?? string.Empty;

        return (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && response.StatusCode == StatusCodes.Status404NotFound
            && !response.HasStarted
            && string.IsNullOrEmpty(response.ContentType)
            && response.ContentLength is null or 0
            && !httpContext.Items.ContainsKey(NotFoundRequestItems.FileHandledKey)
            && !httpContext.Items.ContainsKey(NotFoundRequestItems.RecordedKey)
            && UmbracoOwnedPaths.IsFileRequest(path)
            && !UmbracoOwnedPaths.IsUmbracoOwned(path)
            && !IsWellKnown(path);
    }

    private static bool IsWellKnown(string path) =>
        path.StartsWith(WellKnownPrefix, StringComparison.OrdinalIgnoreCase)
        && (path.Length == WellKnownPrefix.Length || path[WellKnownPrefix.Length] == '/');

    private static async Task HandleAsync(HttpContext httpContext, ILogger logger)
    {
        var services = httpContext.RequestServices;
        var request = httpContext.Request;
        var cancellationToken = httpContext.RequestAborted;
        var path = request.PathBase.Add(request.Path).Value ?? "/";
        var context = new NotFoundRequestContext(path + request.QueryString.Value, request.Host.Host, request.Host.Port, true);

        var domain = MatchDomain(services, context, logger);
        var page = await ResolveAsync(services, context, domain, logger, cancellationToken);
        await RecordAsync(httpContext, context, domain, page is not null, logger, cancellationToken);

        if (page is null)
        {
            return;
        }

        try
        {
            await RenderAsync(httpContext, page, domain);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Rendering the 404 page for {PathAndQuery} failed; the plain 404 is returned.", context.PathAndQuery);
            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.Clear();
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            }
        }
    }

    private static Domain? MatchDomain(IServiceProvider services, NotFoundRequestContext context, ILogger logger)
    {
        try
        {
            return services.GetRequiredService<IRequestDomainMatcher>().Match(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Matching the domain for {PathAndQuery} failed; no domain is used.", context.PathAndQuery);
            return null;
        }
    }

    private static async Task<NotFoundPage?> ResolveAsync(IServiceProvider services, NotFoundRequestContext context, Domain? domain, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            return await services.GetRequiredService<INotFoundPageResolver>().ResolveAsync(context, domain, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Resolving the 404 page for {PathAndQuery} failed; the plain 404 is returned.", context.PathAndQuery);
            return null;
        }
    }

    private static async Task RecordAsync(HttpContext httpContext, NotFoundRequestContext context, Domain? domain, bool pageResolved, ILogger logger, CancellationToken cancellationToken)
    {
        if (!NotFoundRequestItems.TryMarkRecorded(httpContext))
        {
            return;
        }

        var culture = string.IsNullOrWhiteSpace(domain?.Culture) ? null : domain.Culture;
        var occurrence = new NotFoundOccurrence(context.PathAndQuery, domain, culture, context.IsFile, pageResolved);

        try
        {
            await httpContext.RequestServices.GetRequiredService<INotFoundRecorder>().RecordAsync(occurrence, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Recording the 404 for {PathAndQuery} failed.", context.PathAndQuery);
        }
    }

    private static async Task RenderAsync(HttpContext httpContext, NotFoundPage page, Domain? domain)
    {
        var services = httpContext.RequestServices;
        var router = services.GetRequiredService<IPublishedRouter>();

        using var umbracoContextReference = services.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
        var umbracoContext = umbracoContextReference.UmbracoContext;

        var uri = services.GetRequiredService<UriUtility>().UriToUmbraco(new Uri(httpContext.Request.GetEncodedUrl()));
        var builder = await router.CreateRequestAsync(uri);
        if (domain is not null)
        {
            builder.SetDomain(domain as DomainAndUri ?? new DomainAndUri(domain, uri));
        }

        builder.SetPublishedContent(page.Content);
        if (!string.IsNullOrWhiteSpace(page.Culture))
        {
            builder.SetCulture(page.Culture);
        }

        builder.SetIs404();

        var publishedRequest = await router.RouteRequestAsync(builder, new RouteRequestOptions(Umbraco.Cms.Core.Routing.RouteDirection.Inbound));
        umbracoContext.PublishedRequest = publishedRequest;

        var routeValues = await services.GetRequiredService<IUmbracoRouteValuesFactory>().CreateAsync(httpContext, publishedRequest);
        umbracoContext.PublishedRequest = routeValues.PublishedRequest;
        httpContext.Features.Set(routeValues);

        // Routing has set the variation context already. It is set again here, in the method that renders, so the
        // culture also reaches the rendering when the variation context falls back to an AsyncLocal (no request cache).
        services.GetRequiredService<IVariationContextAccessor>().VariationContext =
            new VariationContext(routeValues.PublishedRequest.Culture, routeValues.PublishedRequest.Segment);

        var routeData = new RouteData();
        routeData.Values["controller"] = routeValues.ControllerName;
        routeData.Values["action"] = routeValues.ActionName;
        httpContext.Request.RouteValues = new RouteValueDictionary(routeData.Values);

        var actionContext = new ActionContext(httpContext, routeData, routeValues.ControllerActionDescriptor);
        var invoker = services.GetRequiredService<IActionInvokerFactory>().CreateInvoker(actionContext)
            ?? throw new InvalidOperationException($"No action invoker was created for {routeValues.ControllerName}.{routeValues.ActionName}.");

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        var isHead = HttpMethods.IsHead(httpContext.Request.Method);
        var originalBody = httpContext.Response.Body;
        if (isHead)
        {
            httpContext.Response.Body = Stream.Null;
        }

        try
        {
            await invoker.InvokeAsync();
        }
        finally
        {
            if (isHead)
            {
                httpContext.Response.Body = originalBody;
            }
        }
    }
}
