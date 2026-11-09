# Page Not Found Tracker documentation

**Umbraco.Community.PageNotFoundTracker** records the requests on your Umbraco site that end in a 404, shows your own
404 page for missing pages and files, and lets editors turn the tracked URLs into redirects or block them.

It supports **Umbraco 17.x** on **.NET 10**, with SQLite or SQL Server as the Umbraco database.

## Where to start

| You are | Start with |
|---|---|
| Installing and configuring the package on a site | [Getting started](getting-started.md), then [Configuration](configuration.md) |
| An editor using the dashboards | [The dashboards](dashboards.md) |
| A developer who wants to change how it behaves | [Extending the package](extending/README.md) |
| Looking for the cause of a problem | [Troubleshooting](troubleshooting.md) |

## All pages

### For site implementers

- [Getting started](getting-started.md): requirements, installation, the 404 page, first run and a quick check.
- [Configuration](configuration.md): every setting, its default and an example.
- [How it works](how-it-works.md): how a 404 is detected, tracked, redirected or blocked.
- [Multi-site and languages](multi-site-and-language.md): how domains and cultures decide what happens, with four
  worked setups.
- [Troubleshooting](troubleshooting.md): symptoms, causes and checks.

### For editors

- [The dashboards](dashboards.md): the tracked URLs and the blocked URLs lists, and who can see them.
- [Redirects](redirects.md): turning a tracked URL into a redirect.
- [Blocking](blocking.md): block patterns, status codes, safeguards and precedence.

### For package extenders

- [Extending the package](extending/README.md): an overview of every extension point and the registration rule.
- [404 page resolver](extending/not-found-page-resolver.md): change how the 404 page is chosen.
- [Database providers](extending/database-providers.md): support another database.
- [Redirect providers](extending/redirect-providers.md): send redirects to your own system, and replace the form.
- [Dashboard actions](extending/dashboard-actions.md): add actions to the rows, and change who may use the dashboards.

## Help and source

- Source code, issues and releases: <https://github.com/dawoe/umbraco-page-not-found-tracker>
