# Umbraco Page Not Found Tracker

An Umbraco package that records requests that end in a 404 and, in later versions, lets editors
turn tracked URLs into redirects or block them.

> **Status: foundation only.** This version contains the package plumbing: the solution, the
> build and release pipelines, a placeholder backoffice dashboard and a test site. It does not
> track, redirect or block anything yet. The features below are planned and described in the
> specs in [`docs/superpowers/specs/`](docs/superpowers/specs/).

Supports **Umbraco 17 only** (.NET 10).

## Planned features

- 404 handling for pages and files that belongs to the right site and language (spec 2)
- Tracking of the URLs that visitors request but do not exist (spec 3)
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

The package registers itself through an Umbraco composer, so no other setup is needed.

## Contributing

See [CLAUDE.md](CLAUDE.md) for the solution layout, build and test commands, the test site with its
four launch profiles (single site, multiple sites by path, multiple sites by hostname, multiple
languages), conventions and gotchas. In short: build the client first, then the .NET solution.

```bash
cd src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client
npm ci && npm run build
cd ../../..   # back to src/
dotnet restore && dotnet build && dotnet test
```

The test site databases are committed and contain test-only credentials; restore them with
`git checkout` after running a profile (details in CLAUDE.md).

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
