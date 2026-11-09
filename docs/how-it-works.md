# How it works

This page explains what the package does with a request that ends in a 404: how it is detected, what is
recorded, and how redirects and blocks fit in. For sites with several domains or languages read
[Multi-site and languages](multi-site-and-language.md) as well.

## What happens to a request

For every request the package looks at in this order:

1. **Blocking.** If a [block rule](blocking.md) matches, the request is answered at once with the rule's status code
   and an empty body. Nothing else runs and nothing is tracked.
2. **Umbraco's own handling**: static files, then redirects, then routing. A URL with a redirect registered for it
   (see [Redirects](redirects.md)) is answered with a 301 to its target here, so it never counts as a 404.
3. **No page found**: the request is a 404. The package shows your 404 page, answers with status 404 and records
   the URL. The address in the browser stays the same.

A block rule wins over a redirect, and a redirect wins over the 404 page.

## How a 404 is detected

There are two paths, because Umbraco only routes requests that look like pages.

**A missing page** (`/old-page`). Umbraco finds no content for the URL. The package takes over the final step of
that search and asks which page to show. It chooses your 404 page (see [Configuration](configuration.md)), renders it
with status 404, and records the hit. If no 404 page is configured or found, Umbraco's own 404 behaviour applies
(including its `Error404Collection` setting), and the hit is still recorded.

**A missing file** (`/missing.css`, `/old/brochure.pdf`). A request whose last segment has a file extension is
handled by static files and never reaches Umbraco's routing. The package watches for such requests that end in a
404 with an empty response, and then renders the same 404 page with status 404 and records the hit. Only `GET` and
`HEAD` requests are handled (a `HEAD` request gets the headers without a body). A component that writes its own
response body is left alone.

Details worth knowing:

- Requesting the 404 page's own URL answers 200: it is a normal page.
- An existing page that has no template counts as missing and is answered by the 404 page.
- A protected 404 page shows the login page that protects it, with status 404.

### Paths that are never handled

Paths that belong to Umbraco and the platform are left alone: `/umbraco`, `/App_Plugins`, `/_framework` and
`/.well-known`. They are never answered with your 404 page by the file handling, and never blocked.

## What is tracked

Each missing URL becomes one row, identified by three things together:

- **The path**, normalised: lower case, repeated slashes collapsed, trailing slash removed. `/Old-Page/`, `/old-page`
  and `//old-page` are one URL. With an alias in the domain (`/siteA/old-page`) the full path is kept.
- **The domain** that matched the request, or empty when none matched.
- **The culture** of that domain, or empty.

The same path on two sites, or in two languages, is therefore two rows.

The query string is ignored by default, so `/old?page=1` and `/old?page=2` count towards `/old`. Set
`IncludeQueryString` to track them separately (see [Configuration](configuration.md)).

A row holds:

| Field | Meaning |
|---|---|
| URL | The URL as it was first seen (with its original capitals) |
| Domain, Culture | The matched domain and its culture, or empty |
| Type | A page or a file |
| Hits | How many times it was requested |
| First seen, Last seen | Dates, in UTC |

Requests that are **not** tracked: blocked requests, and everything when `TrackingEnabled` is `false` or the
database is not supported.

## Batching and the flush interval

A request never waits for the database. The package puts each 404 in a queue in memory and a background service
writes the queue to the database every `FlushInterval` (one minute by default). Hits for the same URL are added up
first, so a URL requested a thousand times in a minute is one update.

- The **dashboard is as fresh as the last flush**. It does not refresh itself: reload it, or change a filter, to see
  new data.
- When the database cannot be reached, hits wait in memory and are retried. If more URLs wait than `ChannelCapacity`,
  the least recently seen are dropped and a warning is logged.
- When the queue is full, new hits are dropped and a warning is logged at most once a minute.
- On shutdown the site writes what is left once more. Hits that cannot be written in the time the host allows are
  lost, and the count is logged.

## How redirects and blocks relate

- **Create a redirect** for a tracked page URL: Umbraco gets a redirect and the tracked row is removed. Visitors
  of that URL are redirected from then on. If the redirect is removed again in Umbraco's Redirect URL Management
  dashboard, the URL is tracked again as a new row.
- **Block** a tracked URL (or write a pattern): matching requests get the block's status code with an empty body,
  and every tracked row the pattern covers is removed. When the block is removed the URLs are tracked again from one.

Both actions are done by editors in the dashboard: see [Dashboards](dashboards.md).
