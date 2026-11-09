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

Paths that belong to Umbraco are left alone: `/umbraco`, `/App_Plugins` and `/_framework`. The file handling never
answers them with your 404 page, and a block rule never applies to them. `/.well-known` is also left alone by the file
handling, but it is not protected from block rules.

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

## Clean-up

The tracked URLs table would otherwise only grow. Once a day the package removes every tracked URL whose last hit is
more than `CleanupAfterDays` days ago (365 by default). It is on by default; `CleanupEnabled` switches it off (see
[Configuration](configuration.md#clean-up)).

- **Age is the only rule.** A URL that is still requested never ages out, because every hit moves its *Last seen*
  forward. The number of hits does not protect a URL.
- **The first run is about a day after the package is installed or upgraded**, not at startup, and restarting the
  site does not move it. After that it runs once a day.
- **It runs on one server at a time.** It is an Umbraco distributed background job: Umbraco's `umbracoDistributedJob`
  table makes one server claim each run, also in a load-balanced setup. That server uses its own settings, so keep the
  clean-up settings the same on all servers. During a rolling deployment where some servers do not have the package
  yet, Umbraco can remove and re-add the job, which pushes the next run back by a day.
- **It deletes in batches of 1000**, each a short statement, so tracking and the dashboard keep working while it runs.
  A run continues until everything that is due is gone.
- **A long first run after lowering the age.** Umbraco runs its scheduled jobs one after another on each server, so
  while the clean-up runs, that server does not fire webhooks, run scheduled publishing or Umbraco's own clean-ups.
  A normal daily run is short. Lowering `CleanupAfterDays` on a large table (for example from 365 to 7) makes the
  next run delete a large backlog in one go; lower it in steps on a large table.
- **A removed URL can come back.** When it is requested again it is tracked as a new row, starting from one hit.
  Hits that were still waiting in memory to be written bring the row back the same way.
- **It works with tracking switched off.** With `TrackingEnabled` set to `false`, old URLs are still removed.
- **Blocked URLs are never removed.** The clean-up only touches tracked URLs, not block rules.
- **The log says what it did.** A run that removed URLs logs `Removed <count> tracked 404 URLs last seen before
  <date>.` at information level. A failure is logged as an error and retried at the next daily run.

## How redirects and blocks relate

- **Create a redirect** for a tracked page URL: Umbraco gets a redirect and the tracked row is removed. Visitors
  of that URL are redirected from then on. If the redirect is removed again in Umbraco's Redirect URL Management
  dashboard, the URL is tracked again as a new row.
- **Block** a tracked URL (or write a pattern): matching requests get the block's status code with an empty body,
  and every tracked row the pattern covers is removed. When the block is removed the URLs are tracked again from one.

Both actions are done by editors in the dashboard: see [Dashboards](dashboards.md).
