# 404 page resolver

The resolver decides **which page answers a 404**. Replace it when "the first published page of the configured
document type under the site root" is not how your site works, for example when the 404 page is a fixed page, or
depends on the section of the site the visitor was in.

## The contract

`INotFoundPageResolver` (namespace `Umbraco.Community.PageNotFoundTracker.Core.NotFound`) has one method:
`Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken cancellationToken)`.

| Parameter or result | Meaning |
|---|---|
| `context` | `NotFoundRequestContext(PathAndQuery, Host, Port, IsFile)`: the request that ended in a 404. `Path` is `PathAndQuery` without the query string |
| `domain` | The Umbraco domain the caller already matched, or `null`. The resolver does not match domains itself |
| result | `NotFoundPage(Content, Culture)`: the published page and the culture to render it in (`null` for the default language or an invariant page), or `null` |

Returning `null` means "no page found". For a missing page Umbraco's own 404 behaviour then applies (its
`Error404Collection` setting). For a missing file the answer stays a plain 404 without a body.

## How the package uses it

Both paths ask the resolver: the page path through Umbraco's last-chance finder and the file path through the file
middleware (see [How it works](../how-it-works.md)). An exception is logged and treated as "no page found". A client
that disconnects while the resolver runs falls back to Umbraco's finder.

The default resolver finds the root of the matched domain (or the first root of the content tree when no domain matched), looks for a published
page of the configured document type among its children and then deeper in the tree, and uses the domain's culture
(or the default language). A page that varies by culture without a published variant in that culture counts as not
found.

## Example

The sample answers every 404 with one fixed page, found by its key:

<!-- sample: src/samples/Umbraco.Community.PageNotFoundTracker.Samples/NotFoundPageResolverSample.cs#resolver-class -->
```csharp
/// <summary>
/// Sample resolver: every 404 is answered by one fixed page, chosen by its key, instead of the page found by
/// document type alias under the site root. It must be singleton-compatible: the page finder holds it.
/// </summary>
public sealed class NotFoundPageResolverSample : INotFoundPageResolver
{
    // Replace with the key of your own 404 page.
    private static readonly Guid PageKey = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IPublishedContentCache contentCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundPageResolverSample"/> class.
    /// </summary>
    /// <param name="contentCache">The published content cache.</param>
    public NotFoundPageResolverSample(IPublishedContentCache contentCache) => this.contentCache = contentCache;

    /// <inheritdoc />
    public async Task<NotFoundPage?> ResolveAsync(NotFoundRequestContext context, Domain? domain, CancellationToken cancellationToken)
    {
        var page = await this.contentCache.GetByIdAsync(PageKey, false);

        // null means "not found": Umbraco's default 404 behaviour applies.
        return page is null ? null : new NotFoundPage(page, domain?.Culture);
    }
}
```

## Register it

Register it from a composer marked `[ComposeAfter(typeof(PageNotFoundTrackerComposer))]`, with `AddUnique` so it
replaces the default (see [the registration rule](README.md#the-registration-rule)):

<!-- sample: src/samples/Umbraco.Community.PageNotFoundTracker.Samples/NotFoundPageResolverSampleComposer.cs#resolver-composer -->
```csharp
/// <summary>
/// Registers the sample resolver after the package, so it replaces the default.
/// </summary>
[ComposeAfter(typeof(PageNotFoundTrackerComposer))]
public sealed class NotFoundPageResolverSampleComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) =>
        builder.Services.AddUnique<INotFoundPageResolver, NotFoundPageResolverSample>();
}
```

## What is guaranteed, and what is not

- The resolver is called for requests that Umbraco could not route (pages) or that ended in an empty 404 (files).
  Do not rely on it being called exactly once per request: Umbraco may ask the finder again for a page without a
  template. The hit itself is counted once.
- It is held by a singleton. It must be safe to share between requests: inject singleton services (such as
  `IPublishedContentCache`), and create scoped ones per call if you need them.
- The `domain` is matched by the package and passed in. You do not need to match it again.
- The page you return must be published. Returning an unpublished page is not checked for you.
- Whether the 404 is *recorded* does not depend on the resolver: the hit is counted before the page is rendered.
