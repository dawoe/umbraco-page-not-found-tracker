# Blocking

Some 404s are noise: scanners looking for `/wp-login.php` or `/wp-admin/`. A **block rule** answers such requests at
once with a status code and an empty page, before Umbraco does any work, and they are never tracked.

Blocking needs `UsePageNotFoundTracker()` in `Program.cs` (see [Getting started](getting-started.md)) and a supported
database. Without the call, rules can be saved but nothing is answered.

## Add a rule

**From a tracked URL.** In the **Page not found** dashboard click **Block** on a row (in the **...** menu for a page,
as a button for a file). The dialog opens with that URL's path, domain, culture and status preselected.

**By hand.** In the **Blocked URLs** dashboard click **Add rule**.

In the dialog:

| Field | Meaning |
|---|---|
| Pattern | The path to block, such as `/wp-admin/*`. Required |
| Domain | One Umbraco domain, or *All domains* |
| Culture | One language, or *All cultures* |
| Status code | **403 Forbidden**, **404 Not Found** or **410 Gone** (the default) |

When you confirm, the rule is active at once (on every server) and the tracked URLs it covers are removed from the
list. To remove a rule, click **Unblock** in the **Blocked URLs** dashboard. Requests are then answered normally again
and tracked again from one.

> **A rule applies to every matching request**, not only to missing ones. A page or file that exists is blocked too.

## Pattern syntax

A pattern is matched against the path of the request, below the domain's own path.

| Pattern | Matches | Does not match |
|---|---|---|
| `/wp-login.php` | `/wp-login.php`, `/WP-LOGIN.PHP`, `/wp-login.php?x=1` | `/wp-login.php.bak` |
| `/wp-admin/*` | `/wp-admin`, `/wp-admin/`, `/wp-admin/x/y.php` | `/wp-administrator` |
| `*.php` | `/a.php`, `/a/b/c.php` | `/a.phps` |
| `/old/*/page` | `/old/a/page`, `/old/a/b/page` | `/old/page` |

The rules:

- `*` stands for any run of characters, including `/`.
- A pattern that ends in `/*` also matches the bare path: `/wp-admin/*` matches `/wp-admin`.
- Capitals, a trailing slash and repeated slashes are ignored, in the pattern and in the request.
- The **query string is never matched**: `/wp-login.php?x=1` is blocked by `/wp-login.php`.
- The pattern may be written percent-encoded. It is decoded once, so `%2A` becomes a wildcard.
- With an alias in the domain (`/siteA`), write the pattern **without** it. `/wp-admin/*` blocks
  `/siteA/wp-admin/x` and `/siteB/wp-admin/x` (see [Multi-site and languages](multi-site-and-language.md)).
- At most 800 characters.

Adding a pattern that already exists for the same domain and culture, in any spelling, only updates its status code.

## What visitors get

The status code of the rule with an empty body, for any HTTP method. Nothing else runs: no redirect, no page, no
static file. The request is not tracked, also when tracking is switched off.

## When several rules match

The most specific rule wins, in this order:

1. A rule for the request's own domain beats a rule for all domains.
2. Then a rule for its culture beats a rule for all cultures.
3. Then the rule with the longer fixed start (the part before the first `*`).
4. Then the lowest status code.

A request that matches no Umbraco domain only meets rules for all domains and all cultures.

## Safeguards

The dialog refuses a rule, and shows why, when:

- The pattern covers a path that belongs to Umbraco: *"The pattern covers /umbraco, which belongs to Umbraco and cannot
  be blocked."* This covers `/umbraco`, `/App_Plugins` and `/_framework`, so `/umbraco/*`, `/umbraco/x*` and `/um*`
  are refused.
- The pattern has no character besides `/` and `*`, such as `/*`: *"The pattern must contain at least one character
  besides '/' and '*'."*
- The status code is outside 400 to 499 (only possible through the API).

On top of that, Umbraco's own paths are never blocked while the site runs, whatever a rule says. A pattern such as
`/*.php` is accepted, but it never blocks `/umbraco/anything.php`.

## Where the rules are stored

The rules are stored in the Umbraco database and managed through the dashboard. There is no configuration file for
them, and all servers of a load-balanced site share them.

If a block does not match, see [Troubleshooting](troubleshooting.md).
