# Configuration

All settings are optional. They live in one section of `appsettings.json`:

```
Umbraco:Community:PageNotFoundTracker
```

Settings are read once when the site starts. **A change needs a restart.**

Environment variables work as well, with double underscores instead of colons, for example
`Umbraco__Community__PageNotFoundTracker__FlushInterval=00:00:30`. A list item gets an index:
`Umbraco__Community__PageNotFoundTracker__DashboardUserGroups__0=writer`.

## The 404 page

| Setting | Type | Default | Effect |
|---|---|---|---|
| `NotFoundPageDocumentTypeAlias` | string | empty | Alias of the document type of your 404 page. The package looks for a published page of that type directly under the site root (the root of the matched domain, or the first root without a domain) and then deeper in the tree. Empty means no custom 404 page: Umbraco's own 404 behaviour applies. |

Example: `"NotFoundPageDocumentTypeAlias": "error"`. See [Getting started](getting-started.md) for creating the
page.

## Tracking

| Setting | Type | Default | Effect |
|---|---|---|---|
| `TrackingEnabled` | boolean | `true` | When `false` nothing is recorded and nothing is written to the database. The 404 page, the dashboards and blocking keep working. The tracking tables are still created. |
| `FlushInterval` | time span | `00:01:00` | How often recorded 404s are written to the database, as `hh:mm:ss`. The minimum is one second; zero or a negative value means the default. A short interval shows new 404s in the dashboard sooner, but writes more often. |
| `IncludeQueryString` | boolean | `false` | When `true` the query string is part of the tracked URL, so `/old?page=1` and `/old?page=2` are two URLs. When `false` they count as one. URLs with a query string cannot get a redirect from the dashboard. |
| `ChannelCapacity` | integer | `10000` | How many 404s the in-memory queue holds between writes. When it is full, new 404s are dropped (a warning is logged at most once a minute). A value below 1 means the default. |

Examples: `"FlushInterval": "00:00:05"`, `"IncludeQueryString": true`, `"ChannelCapacity": 20000`.

How tracking works is described in [How it works](how-it-works.md).

## The dashboards

| Setting | Type | Default | Effect |
|---|---|---|---|
| `DashboardUserGroups` | list of strings | empty | Aliases of the user groups that may use the two dashboards and their API. Empty means the `admin` and `editor` groups. |

The rules:

- The `admin` group always has access, so a wrong setting can never lock out the person who fixes it.
- A user passes when they are in any one of the listed groups.
- Aliases are matched case-insensitively, and surrounding spaces are ignored.
- An empty list behaves exactly like not setting it.
- A list with only blank entries falls back to `admin` and `editor` and logs a warning.
- The dashboards are part of the Content section, so a user also needs access to that section.

Example: `"DashboardUserGroups": ["admin", "editor", "writer"]`.

## Full example

```json
{
  "Umbraco": {
    "Community": {
      "PageNotFoundTracker": {
        "NotFoundPageDocumentTypeAlias": "error",
        "TrackingEnabled": true,
        "FlushInterval": "00:05:00",
        "IncludeQueryString": true,
        "ChannelCapacity": 20000,
        "DashboardUserGroups": [ "admin", "editor", "writer" ]
      }
    }
  }
}
```
