# Extending the package

The package works without any code on your side. When its behaviour does not fit your site you can replace parts of it.
Each extension point below has its own page with the contract, an example that is compiled with the package, and
what is and is not guaranteed.

| Extension point | Replaces | For | Page |
|---|---|---|---|
| `INotFoundPageResolver` | How the 404 page is chosen | A site with its own rules for which page answers a 404 | [404 page resolver](not-found-page-resolver.md) |
| `ITrackingDatabaseProvider` (and its optional `Upsert`) | Which databases tracking and blocking support | A site on a database other than SQLite or SQL Server | [Database providers](database-providers.md) |
| `IRedirectProvider` | Where a redirect is created | A site with its own redirect system | [Redirect providers](redirect-providers.md) |
| `pnftRedirectForm` extension type | The form in the Create redirect dialog | A custom provider that needs other input than a target page | [Redirect providers](redirect-providers.md) |
| Entity actions on the dashboard rows | The buttons on the rows | Extra actions on tracked URLs or block rules | [Dashboard actions](dashboard-actions.md) |
| `IDashboardAccess` | Who may use the dashboards | An access rule that cannot be written as user groups | [Dashboard actions](dashboard-actions.md#who-may-use-the-dashboards) |

## The registration rule

Register every replacement from a composer marked `[ComposeAfter(typeof(PageNotFoundTrackerComposer))]`.

Umbraco runs composers in order. Every composer of this package runs **before** `PageNotFoundTrackerComposer`, so by
the time your composer runs all package registrations exist. Replaceable services are registered with Umbraco's
`AddUnique`, where the last registration wins. A registration made *before* the package composers is therefore
overwritten by the package, and a composer without the attribute may run too early.

Database providers are the one exception to `AddUnique`: they are added with `AddSingleton`, and the **last**
registration for a provider name is the one used.

## What you can rely on

- The interfaces and records named on these pages, the entity type names and the `pnftRedirectForm` extension type
  are public and only change with a major version.
- Everything else in the package (internal services, the collection elements and contexts of the dashboards, the
  database model beyond what `ITrackingDatabaseProvider` needs) is not an extension point and can change at any time.

## The samples

The example code on these pages is taken from real files that are compiled together with the package:

- C#: `src/samples/Umbraco.Community.PageNotFoundTracker.Samples/`
- TypeScript: `src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/samples/`

A check fails the build when a page and its sample drift apart.
