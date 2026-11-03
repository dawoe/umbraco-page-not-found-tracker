# Umbraco Page Not Found Tracker

An Umbraco package that records requests that end in a 404 and, in later versions, lets editors
turn tracked URLs into redirects or block them.

This version shows the configured 404 page for missing pages and files, and records every 404 URL with a
hit count in the Umbraco database (SQLite or SQL Server). There is no screen to view the tracked URLs yet
(the backoffice dashboard is a placeholder); a dashboard, redirects and blocking are planned for upcoming
versions.

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

On first start the package creates its tables (`pageNotFoundTrackerUrl`) in the Umbraco database. On other
databases than SQLite and SQL Server tracking stays off with an error in the log; the site keeps working.

## Settings

Optional, under `Umbraco:Community:PageNotFoundTracker` in `appsettings.json`, read at startup:

| Setting | Default | Meaning |
|---|---|---|
| `NotFoundPageDocumentTypeAlias` | empty | Document type alias of the 404 page under the site root. Empty: Umbraco's own 404 behaviour. |
| `TrackingEnabled` | `true` | Record 404 URLs. |
| `FlushInterval` | `00:01:00` | How often recorded 404s are written to the database (minimum one second). |
| `IncludeQueryString` | `false` | Treat URLs that differ only in their query string as different URLs. |
| `ChannelCapacity` | `10000` | Maximum number of 404s held in memory between writes; beyond it new ones are dropped with a warning. |

## Links

- Source, issues and documentation: https://github.com/dawoe/umbraco-page-not-found-tracker
- License: MIT
