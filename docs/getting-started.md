# Getting started

This guide takes an empty Umbraco 17 site to a working tracker.

## Requirements

- Umbraco 17.x on .NET 10.
- SQLite or SQL Server as the Umbraco database. Both work out of the box. With any other database the site keeps
  working but nothing is tracked (see [An unsupported database](#an-unsupported-database)).

## 1. Install the package

```bash
dotnet add package Umbraco.Community.PageNotFoundTracker
```

One package contains everything: the 404 handling, tracking, both dashboards and both database providers.

## 2. Add the middleware

Call `UsePageNotFoundTracker()` in `Program.cs`, **after** `BootUmbracoAsync()` and **before** `UseUmbraco()`:

<!-- sample: test-sites/Website-V17/Program.cs#program-pipeline -->
```csharp
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
```

The call needs `using Umbraco.Community.PageNotFoundTracker.Extensions;` at the top of the file.

It is the only call to add. It registers two pieces of middleware at once:

- **Blocking** runs first, ahead of static files, redirects and routing, so a blocked URL is answered immediately
  (see [Blocking](blocking.md)).
- **File 404 handling** shows your 404 page for missing files such as `/missing.css`.

Placed after `UseUmbraco()` the middleware does nothing. Without the call, 404 pages for missing *pages* still work,
but missing files, and blocking, do not.

## 3. Create the 404 page

The package answers a 404 with a page from your own content tree.

1. Create a document type with a template for your 404 page, for example with the alias `error`. The Clean starter kit
   already has one.
2. Create and **publish** a page of that type under the root of each site. A multi-language site needs the page
   published in each language.
3. Tell the package the alias in `appsettings.json`:

```json
{
  "Umbraco": {
    "Community": {
      "PageNotFoundTracker": {
        "NotFoundPageDocumentTypeAlias": "error"
      }
    }
  }
}
```

Without the setting nothing changes for visitors: Umbraco's own 404 behaviour applies, and tracking still works. All
settings are listed in [Configuration](configuration.md). How the page is chosen for sites with several domains or
languages is explained in [Multi-site and languages](multi-site-and-language.md).

## 4. First run

The package keeps its own tables in the Umbraco database (`pageNotFoundTrackerUrl` and
`pageNotFoundTrackerBlockRule`) and creates them itself with migrations. It waits until Umbraco has finished
installing and reached its normal running state, so there is nothing to run by hand:

- With **unattended install** the tables are created in the same start. A fresh database logs a few harmless
  "database not available" lines first.
- With the **installer screen**, finish the installer. The tables are created once Umbraco is running.

404s that arrive while the tables are still being created are kept in memory and written as soon as they exist.

## 5. Check that it works

1. Request a page that does not exist, for example `https://your-site/this-page-does-not-exist`. You see your 404
   page, with status 404.
2. Request a file that does not exist, for example `https://your-site/missing.css`. You see the same 404 page.
3. Wait one flush interval. By default recorded 404s are written to the database once a minute. For a quick try-out
   set `"FlushInterval": "00:00:05"`.
4. Open the backoffice, go to the **Content** section and open the **Page not found** dashboard. Both URLs are
   listed with a hit count. If the dashboard is missing, see [Troubleshooting](troubleshooting.md).

## An unsupported database

The package ships tracking support for SQLite and SQL Server. On another database the site keeps working and shows
your 404 page, but tracking is off and an error naming the database provider is logged at startup. You can add your
own database support: see [Database providers](extending/database-providers.md).

## Several servers (load balancing)

- Every server records its own 404s in memory and writes them to the shared database, where the counts add up.
- Blocking rules and tracked URLs live in the database. When an editor saves or removes a block rule, the other
  servers refresh their rules through Umbraco's own cache synchronisation, so Umbraco load balancing must be set up as
  usual.
- Settings are read per server. Give every server the same configuration.
- On a new deployment let one server finish starting before you start the others, as you would for an Umbraco upgrade.

## Next steps

- [Configuration](configuration.md) to tune tracking and dashboard access.
- [Dashboards](dashboards.md) for what editors can do with the tracked URLs.
- [How it works](how-it-works.md) to understand what is tracked and when.
