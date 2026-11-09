# Redirect providers

When an editor clicks **Create redirect**, the package asks the active **redirect provider** to create the redirect.
The default provider writes to Umbraco's own redirect management. Replace it to send redirects to a system of your own.
You can also replace the **form** in the dialog, when your provider needs other input than a target page.

## The provider contract

`IRedirectProvider` (namespace `Umbraco.Community.PageNotFoundTracker.Core.Redirects`):

| Member | Meaning |
|---|---|
| `bool CanHandle(TrackedUrl url)` | Whether this provider can create a redirect for the row. Decides if the row shows **Create redirect**, and guards the endpoint (a row it cannot handle gets `409`) |
| `Task<RedirectResult> CreateAsync(TrackedUrl url, JsonElement? payload, CancellationToken cancellationToken)` | Create the redirect. `payload` is whatever the form returned |

`RedirectResult.Success()` and `RedirectResult.Failure(message)` are the two results. The message of a failure is shown
to the editor inside the dialog, and nothing else happens.

`TrackedUrl` carries what identifies the request: `RelativePath` (the path without the domain's own part), `Domain`,
`Culture`, `IsFile`, `DisplayUrl` and the counters.

## What the package still does for you

- Shows the action only on rows for which `CanHandle` is `true`.
- Checks that the user may use the dashboard (the access policy), and answers `409` for a row you cannot handle.
- Calls your provider, and turns a failure into an inline message (`422`). An exception is logged, and the editor sees a
  generic text, never the exception.
- After a success, **removes the tracked URL** and the hits for it that are still waiting to be written, so the row
  cannot come back. If removing fails the redirect still exists, and the editor sees a warning.
- Reloads the list.

## Example: a provider

A provider that reads `{ "targetUrl": "/somewhere" }` from the payload:

<!-- sample: src/samples/Umbraco.Community.PageNotFoundTracker.Samples/RedirectProviderSample.cs#redirect-provider-class -->
```csharp
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
```

Register it from a composer marked `[ComposeAfter(typeof(PageNotFoundTrackerComposer))]`, with `AddUnique` (see
[the registration rule](README.md#the-registration-rule)):

<!-- sample: src/samples/Umbraco.Community.PageNotFoundTracker.Samples/RedirectProviderSampleComposer.cs#redirect-provider-composer -->
```csharp
/// <summary>
/// Registers the sample redirect provider after the package, so it replaces the default.
/// </summary>
[ComposeAfter(typeof(PageNotFoundTrackerComposer))]
public sealed class RedirectProviderSampleComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) =>
        builder.Services.AddUnique<IRedirectProvider, RedirectProviderSample>();
}
```

## The form

The dialog shows one form: of all registered `pnftRedirectForm` extensions the one with the highest `weight` wins. The
package's default form (a page picker) has weight **-100**, so any form with a weight of 0 or more replaces it.

A form is a custom element with:

- a `trackedUrl` property, set by the dialog: the row the redirect is for;
- `getPayload(): Promise<unknown | undefined>`, called when the editor confirms. Return the payload for your provider, or
  `undefined` when the form is not valid (the form shows its own message and nothing is sent).

The form never calls the API itself.

The manifest:

<!-- sample: src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/samples/redirect-form.manifests.ts#redirect-form-manifest -->
```typescript
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'pnftRedirectForm',
    alias: 'My.RedirectForm',
    name: 'My redirect form',
    weight: 10, // anything above -100 beats the package's default form
    element: () => import('./redirect-form.element.js'),
  },
];
```

The element, which matches the sample provider above:

<!-- sample: src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/samples/redirect-form.element.ts#redirect-form-element -->
```typescript
@customElement('my-redirect-form')
export class MyRedirectFormElement extends UmbLitElement implements PnftRedirectFormElement {
  // Set by the create redirect modal: the dashboard row the redirect is created for.
  @property({ attribute: false })
  trackedUrl?: PnftTrackedUrlCollectionModel;

  @state()
  private _targetUrl = '';

  // Called when the editor confirms. Return the payload the redirect provider reads, or undefined when the form is
  // not valid (nothing is sent). The form never calls the API itself.
  async getPayload(): Promise<unknown | undefined> {
    return this._targetUrl.startsWith('/') ? { targetUrl: this._targetUrl } : undefined;
  }

  override render() {
    return html`
      <uui-input
        label="Target URL"
        placeholder="/somewhere"
        .value=${this._targetUrl}
        @input=${(event: Event) => (this._targetUrl = (event.target as HTMLInputElement).value)}></uui-input>
    `;
  }
}

export default MyRedirectFormElement; // the element loader needs a default export
```

Register the manifest like any backoffice extension, for example with an `umbraco-package.json` in
`App_Plugins` (see [Umbraco's documentation on extensions](https://docs.umbraco.com/umbraco-cms/customizing/extending-overview)).

In your own project the sample's two type imports (`PnftRedirectFormElement` and `PnftTrackedUrlCollectionModel`) come
from the package's source and are not published as types: declare the two members you use (`trackedUrl` and
`getPayload`) yourself. The package declares the `pnftRedirectForm` type in TypeScript only; if
TypeScript does not know it, declare it for your project or cast the manifest.

## What is guaranteed, and what is not

- A provider is held by a singleton and asked about every row of the list, so it must be safe to share and `CanHandle`
  must be cheap.
- The payload reaches `CreateAsync` as sent, or `null` when the form sent none. The package does not validate it.
- The tracked URL is removed only after `CreateAsync` succeeds.
- In a setup with several servers, another server's queued hits for the same URL can recreate the row after it was
  removed. It then shows up as a new row.
- The default provider's rules (target must be published, no query strings) belong to the default provider. Your
  provider decides its own.
