# Umbraco Page Not Found Tracker

An Umbraco package that records requests that end in a 404 and, in later versions, lets editors
turn tracked URLs into redirects or block them.

> **Status: in development.** This version shows the configured 404 page for missing pages and
> files, and records every 404 URL with a hit count in the Umbraco database (SQLite or SQL Server).
> There is no screen to view the tracked URLs yet (the backoffice dashboard is a placeholder), and
> nothing is redirected or blocked. The planned features are described in the specs in
> [`docs/superpowers/specs/`](docs/superpowers/specs/).

Supports **Umbraco 17 only** (.NET 10).

## Features

- 404 handling for pages and files that belongs to the right site and language (spec 2)
- Tracking of the URLs that visitors request but do not exist (spec 3): one row per URL, domain and
  culture in the table `pageNotFoundTrackerUrl`, written in batches by a background service

## Planned features

- A backoffice dashboard to browse tracked URLs (spec 4)
- Creating redirects from tracked URLs (spec 5)
- Blocking noisy URLs, such as scanner requests (spec 6)
- Documentation for implementers (spec 7)

## Installation

```bash
dotnet add package Umbraco.Community.PageNotFoundTracker
```

Then call `UsePageNotFoundTracker()` in `Program.cs`, after `BootUmbracoAsync()` and before
`UseUmbraco()`:

```csharp
using Umbraco.Community.PageNotFoundTracker.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

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

The package registers itself through an Umbraco composer, so no other setup is needed. On first start
it creates its tables in the Umbraco database (SQLite or SQL Server; other databases leave tracking off
with an error in the log, and the site keeps working).

## Settings

All settings are optional and live under `Umbraco:Community:PageNotFoundTracker` in `appsettings.json`.
They are read at startup; changing them needs a restart.

```json
{
  "Umbraco": {
    "Community": {
      "PageNotFoundTracker": {
        "NotFoundPageDocumentTypeAlias": "error",
        "TrackingEnabled": true,
        "FlushInterval": "00:01:00",
        "IncludeQueryString": false,
        "ChannelCapacity": 10000
      }
    }
  }
}
```

| Setting | Default | Meaning |
|---|---|---|
| `NotFoundPageDocumentTypeAlias` | empty | Document type alias of the 404 page, looked up under the site root of the request. Empty: Umbraco's own 404 behaviour. |
| `TrackingEnabled` | `true` | Record 404 URLs. |
| `FlushInterval` | `00:01:00` | How often recorded 404s are written to the database (minimum one second). |
| `IncludeQueryString` | `false` | Treat URLs that differ only in their query string as different URLs. |
| `ChannelCapacity` | `10000` | Maximum number of 404s held in memory between writes; beyond it new ones are dropped with a warning in the log. |

URLs are compared ignoring case, a trailing slash and repeated slashes.

## Contributing

See [CLAUDE.md](CLAUDE.md) for the solution layout, build and test commands, the test site with its
launch profiles (single site, multiple sites by path, multiple sites by hostname, multiple
languages, SQL Server), the tracking database providers and migration scripts, conventions and
gotchas. In short: build the client first, then the .NET solution.

```bash
cd src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client
npm ci && npm run build
cd ../../..   # back to src/
dotnet restore && dotnet build && dotnet test
```

The test site databases are committed and contain test-only credentials; restore them with
`git checkout` after running a profile (details in CLAUDE.md).

The `sql-server` launch profile uses a LocalDB instance named `pnft-test` (Windows, no Docker needed):
`sqllocaldb create pnft-test 15.0 -s`, then `dotnet run --launch-profile sql-server`. To use Docker instead
(test-only password, never reuse it), start a container and override the connection string, see CLAUDE.md:

```bash
docker run -d --name pnft-sqlserver -e ACCEPT_EULA=Y -e 'MSSQL_SA_PASSWORD=PnftTest-only1!' \
  -p 14330:1433 mcr.microsoft.com/mssql/server:2022-latest
```

The SQL Server integration tests need a SQL Server too. For the tests, set `PNFT_SQLSERVER_CONNECTION` to a server-level connection string (no database), for example
`Server=localhost,14330;User Id=sa;Password=PnftTest-only1!;TrustServerCertificate=True`; without it the SQL
Server tests are skipped. A LocalDB alternative is in CLAUDE.md.

## Branches

- `develop` is the default and integration branch. Pull requests target `develop`.
- `main` is the release branch. Releases are published from `main` only.

## Maintainers: secrets and repository settings

The workflows in `.github/workflows/` need the following to exist. They are not created by the repository.

| What | Used by | Notes |
|---|---|---|
| Secret `MYGET_API_KEY` | `beta-release.yml` (push to `develop`) | API key for the MyGet feed `dawoe-umbraco` |
| Secret `NUGET_USER` | `release.yml` (manual, `main` only) | NuGet.org username; NuGet.org must have a trusted publishing policy for this repository and workflow (OIDC login, no API key stored) |
| Default branch `develop` | all | `main` is the release branch |

## License

MIT
