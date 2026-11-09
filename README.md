# Umbraco Page Not Found Tracker

An Umbraco package that records the requests on your site that end in a 404, shows your own 404 page for
missing pages and files, and lets editors turn the tracked URLs into redirects or block them.

Supports **Umbraco 17 only** (.NET 10), with SQLite or SQL Server as the Umbraco database.

## Features

- **404 handling** for pages and files, in the right site and language.
- **Tracking** of every URL that visitors request but does not exist: one row per URL, domain and culture with a hit
  count, written in batches by a background service.
- **Automatic clean-up** of tracked URLs nobody requested for a year (configurable), once a day, on one server at a
  time.
- **A backoffice dashboard** in the Content section to search, sort, filter and page through the tracked URLs.
- **Redirects**: turn a tracked URL into an Umbraco redirect with one action.
- **Blocking**: answer scanner noise such as `/wp-admin/*` at once with a 403, 404 or 410, without tracking it.
- **Extension points** to replace the 404 page resolver, the redirect backend and form, the database provider and the
  dashboard row actions.

## Documentation

The documentation is in [`docs/`](docs/README.md):

- [Getting started](docs/getting-started.md) and [Configuration](docs/configuration.md) to install and configure it.
- [The dashboards](docs/dashboards.md) for editors.
- [Extending the package](docs/extending/README.md) for developers.
- [Troubleshooting](docs/troubleshooting.md).

## Installation

```bash
dotnet add package Umbraco.Community.PageNotFoundTracker
```

Then call `UsePageNotFoundTracker()` in `Program.cs`, after `BootUmbracoAsync()` and before `UseUmbraco()`. The package
creates its tables on first start. See [Getting started](docs/getting-started.md) for the full steps.

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
