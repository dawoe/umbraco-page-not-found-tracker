# Troubleshooting

Find your symptom, check the likely causes in order, and apply the fix. Most checks only need the site log and
`appsettings.json`. Settings are explained in [Configuration](configuration.md).

## Nothing shows up in the dashboard

The **Page not found** dashboard says *No 404s tracked yet*, or a URL you requested is missing.

1. **Wait one flush interval.** New 404s are written to the database every `FlushInterval` (one minute by default),
   and the dashboard does not refresh itself. Wait, then reload the page.
2. **Is tracking on?** `TrackingEnabled` must not be `false`. With it off, the 404 page still works but nothing is
   recorded.
3. **Is the URL really a 404?** A path that exists, or that is redirected, is not tracked. Test with a path that
   exists nowhere (`/pnft-test-123`). In a multi-site setup a path without a site prefix may fall back to the first site
   and answer 200 (see [Multi-site and languages](multi-site-and-language.md)).
4. **Is the URL blocked?** A [blocked URL](blocking.md) is never tracked. Look in the **Blocked URLs** dashboard.
5. **Is it a file that answered with a body?** A missing *file* is only tracked when the request ends in an empty 404.
   A component of your site that writes its own 404 body for `/something.pdf` is left alone.
6. **Did the tables get created?** The dashboard shows *Tracking database unsupported or unavailable*, or the log has
   a line starting with `Page Not Found Tracker:`:
   - *"no database provider is registered for '...'"*: your database is not SQLite or SQL Server. See
     [Database providers](extending/database-providers.md).
   - *"migrating the tracking tables failed"*: read the exception below the line (permissions of the database user,
     a locked database). Fix the cause and restart the site. There is no retry in the same run.
7. **Did Umbraco finish installing?** The package waits until Umbraco is running. On a site that still shows the
   installer, nothing is tracked yet.
8. **Is the queue full?** A flood of 404s beyond `ChannelCapacity` is dropped, with a warning in the log.

## A tracked URL disappeared

A URL that was in the **Page not found** dashboard is gone.

1. **The clean-up removed it.** URLs whose last hit is more than `CleanupAfterDays` days ago (365 by default) are
   removed once a day. The log has a line `Removed <count> tracked 404 URLs last seen before <date>.` Raise the
   setting, or set `CleanupEnabled` to `false`, to keep them longer (see [Clean-up](how-it-works.md#clean-up)).
2. **Someone created a redirect for it.** Creating a redirect removes the row. Look in Umbraco's Redirect URL
   Management dashboard (see [Redirects](redirects.md)).
3. **A block rule covers it.** Saving a block removes every row the pattern covers. Look in the **Blocked URLs**
   dashboard (see [Blocking](blocking.md)).

When the URL is requested again it comes back as a new row, starting from one hit.

## The 404 page does not appear

You see Umbraco's plain 404 page, a blank page or the wrong site's page instead of your own.

1. **Is the alias set and correct?** `NotFoundPageDocumentTypeAlias` must be the alias of the document type (not the
   name). With an empty or unknown alias, Umbraco's own 404 page is shown. The status is still 404 and the hit is
   still tracked.
2. **Is the page published?** The page must exist under the root of the site *and* be published. Unpublished pages are
   skipped.
3. **Is it under the right root?** The package looks under the root of the Umbraco domain that matched the request,
   or under the first root when no domain matched. A site needs its own 404 page.
4. **Languages.** A page that varies by language needs a published variant in the language of the domain. Without
   it, the page counts as not found.
5. **Domains.** In a multi-site setup check that the domains include the port when the site does not run on 80 or
   443. A request that matches no domain gets the first site's page.
6. **Missing files only (`/missing.css`)?** Check that `UsePageNotFoundTracker()` is in `Program.cs`, after
   `BootUmbracoAsync()` and **before** `UseUmbraco()`. Placed after `UseUmbraco()` it does nothing, and missing files
   then get Umbraco's default answer. See [Getting started](getting-started.md).
7. **A custom resolver?** If you replaced the resolver, check it returns a published page. See
   [404 page resolver](extending/not-found-page-resolver.md).
8. **A protected page?** A 404 page that is protected shows the login page that protects it. Do not protect the 404
   page.

## A redirect was created but is not applied

1. **Was the row a page without a query string?** Rows for files, and rows with a query string, cannot get a
   redirect. They have no **Create redirect** action, because Umbraco's redirects never match a query string.
2. **Check Umbraco's Redirect URL Management dashboard** (Content section). The redirect is listed there with its
   target. If it is not, the dialog showed an error.
3. **Is the target published?** The dialog refuses an unpublished target (*"The target page does not exist or is not
   published."*, and for languages *"The target page is not published in ..."*).
4. **Does the domain and language match?** A redirect is registered for the domain and language of the row. A request
   on another site, or in another language, does not match it. See
   [Multi-site and languages](multi-site-and-language.md).
5. **Is a block rule in the way?** A matching [block rule](blocking.md) is answered before redirects run.

Redirect handling cannot be switched off in Umbraco 17. If you changed the redirect backend, check your own provider
(see [Redirect providers](extending/redirect-providers.md)).

## A block does not match

1. **Write the path without the site's own part.** The pattern is matched against the path *below* the domain. With the
   domain `localhost/siteA`, the pattern `/wp-admin/*` blocks `/siteA/wp-admin/x`, and the pattern
   `/siteA/wp-admin/*` blocks nothing.
2. **Check the scope.** A rule for one domain or one culture only applies there. Set both to *All* to test.
3. **Check the wildcard.** `*` is needed for "everything below": `/wp-admin` matches only that path, while
   `/wp-admin/*` also matches everything under it. See the examples in [Blocking](blocking.md).
4. **Capitals, a trailing slash and the query string are ignored**, so they cannot be why a rule does not match. A
   rule can never match on the query string.
5. **Umbraco's own paths are never blocked** (`/umbraco`, `/App_Plugins`, `/_framework`).
6. **Is `UsePageNotFoundTracker()` called?** Without it the rules are saved but nothing is answered.
7. **Is the database supported?** On an unsupported database blocking is off for the whole run (one warning is
   logged).
8. **Several servers.** A new rule reaches the other servers through Umbraco's cache synchronisation. If a server
   still lets the URL through, check that load balancing is configured.

A more specific rule may also win over yours: see *When several rules match* in [Blocking](blocking.md).

## The dashboards are not visible

1. **User group.** The dashboards are for the groups in `DashboardUserGroups`, by default **Administrators** and
   **Editors**. The `admin` group always has access. A user in another group does not see the tabs.
2. **Content section.** The dashboards live in the Content section, so the user needs access to it.
3. **Restart.** A change of `DashboardUserGroups` is read at startup.
4. **Aliases, not names.** The setting takes group *aliases* (`editor`), not display names ("Editors"). A list with
   only blank entries falls back to the defaults and logs a warning.
5. **Reload.** The tabs appear after the backoffice has asked the server whether you may see them. If a tab is missing
   on a fresh page load, reload once.
6. **Package installed?** Check that `Umbraco.Community.PageNotFoundTracker` is referenced by the site project you
   run, and that its assets are published (`App_Plugins/PageNotFoundTracker` is served).
