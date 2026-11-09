# Umbraco Page Not Found Tracker

An Umbraco package that records the requests on your site that end in a 404, shows your own 404 page for missing pages
and files, and lets editors turn the tracked URLs into redirects or block them.

- **404 handling** for pages and files, in the right site and language.
- **Tracking** of every URL that does not exist, with a hit count per URL, domain and culture (SQLite or SQL Server).
- **A backoffice dashboard** in the Content section to search, sort and filter the tracked URLs.
- **Redirects** and **blocking** straight from the dashboard.

Supports **Umbraco 17** only.

## Installation

```bash
dotnet add package Umbraco.Community.PageNotFoundTracker
```

Call `UsePageNotFoundTracker()` in `Program.cs`, after `BootUmbracoAsync()` and before `UseUmbraco()`:

```csharp
using Umbraco.Community.PageNotFoundTracker.Extensions;

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

app.UsePageNotFoundTracker();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
```

On first start the package creates its tables in the Umbraco database. On other databases than SQLite and SQL Server
tracking stays off with an error in the log; the site keeps working.

## Settings

Optional, under `Umbraco:Community:PageNotFoundTracker` in `appsettings.json`, read at startup:

| Setting | Default | Meaning |
|---|---|---|
| `NotFoundPageDocumentTypeAlias` | empty | Document type alias of the 404 page under the site root. Empty: Umbraco's own 404 behaviour. |
| `TrackingEnabled` | `true` | Record 404 URLs. |
| `FlushInterval` | `00:01:00` | How often recorded 404s are written to the database (minimum one second). |
| `IncludeQueryString` | `false` | Treat URLs that differ only in their query string as different URLs. |
| `ChannelCapacity` | `10000` | Maximum number of 404s held in memory between writes; beyond it new ones are dropped with a warning. |
| `DashboardUserGroups` | empty | Aliases of the user groups that may use the dashboards. Empty: `admin` and `editor`. |

All settings, with examples, are described in the
[configuration reference](https://github.com/dawoe/umbraco-page-not-found-tracker/blob/develop/docs/configuration.md).

## Documentation

- [Getting started](https://github.com/dawoe/umbraco-page-not-found-tracker/blob/develop/docs/getting-started.md)
- [All documentation](https://github.com/dawoe/umbraco-page-not-found-tracker/blob/develop/docs/README.md): the
  dashboards, redirects, blocking, extension points and troubleshooting.

## Links

- Source and issues: https://github.com/dawoe/umbraco-page-not-found-tracker
- License: MIT
