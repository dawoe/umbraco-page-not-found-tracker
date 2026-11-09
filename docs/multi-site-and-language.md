# Multi-site and languages

Everything the package does follows Umbraco's **domains** (Culture and Hostnames). The domain that matches a request
decides which 404 page is shown, what is recorded, where a redirect is registered and which block rules apply.

This page explains the rules, then walks through the four setups of the package's own test site. The examples use
its ports, and each was checked against the running site.

## The rules

**Which domain matches.** Umbraco compares the host name (case-insensitive), the port, and the path of the request. When
several domains fit, the one with the longest matching path wins. A request that matches no domain has no domain.

**Which 404 page.** The package looks for the page of your 404 document type under the root of the matched domain.
With no domain it uses the first site root. The page is rendered in the domain's culture, or in the default
language when the domain has none. A page that varies by language needs a published variant in that culture,
otherwise the page counts as not found.

**What is recorded.** The row stores the full path (`/siteb/old`), the path without the domain's own part
(`/old`), the matched domain name in lower case including its port and path (`localhost:44402/siteb`), and the
culture. With no match, domain and culture are empty.

**Redirects** are registered for the matched domain's site, so the same path on two sites can redirect to two
different pages. See [Redirects](redirects.md).

**Block rules** match the path without the domain's own part, so one pattern such as `/wp-admin/*` covers
`/siteA/wp-admin/x` and `/siteB/wp-admin/x`. A rule can be limited to one domain and one culture. See
[Blocking](blocking.md).

**Requests that match no domain** fall back to the first site and the default language, the same way Umbraco
routes them. This is not a 404 by itself: a path that exists on that site answers 200. Choose a path that exists
nowhere when you test a 404.

## One site

`https://localhost:44401/`, no domains.

| Request | Answer | Tracked as |
|---|---|---|
| `/nope` | 404, the 404 page | URL `/nope`, domain and culture empty |
| `/missing.css` | 404, the same 404 page | URL `/missing.css`, a file |
| `/about` | 200 | not tracked |

## Several sites told apart by a path (aliases)

Two sites, "Site A" and "Site B", on `https://localhost:44402` with the domains `localhost:44402/siteA` and
`localhost:44402/siteB`.

| Request | Answer | Tracked as |
|---|---|---|
| `/siteA/nope` | 404, "Page not found - Site A" | URL `/sitea/nope`, relative path `/nope`, domain `localhost:44402/sitea` |
| `/siteB/nope` | 404, "Page not found - Site B" | URL `/siteb/nope`, relative path `/nope`, domain `localhost:44402/siteb` |
| `/siteB/missing.css` | 404, Site B's page | a file, domain `localhost:44402/siteb` |
| `/nope` | 404, Site A's page (no domain matches) | URL `/nope`, domain empty |
| `/siteC/nope` | 404, Site A's page | URL `/sitec/nope`, domain empty |

`/siteA/nope` and `/siteB/nope` are two rows even though the relative path is the same.

## Several sites told apart by host name

Two sites on `www.siteA.local` and `www.siteB.local`, both on port 44404.

| Request | Answer | Tracked as |
|---|---|---|
| `https://www.siteA.local:44404/nope` | 404, Site A's page | URL `/nope`, domain `www.sitea.local:44404` |
| `https://www.siteB.local:44404/missing.css` | 404, Site B's page | a file, domain `www.siteb.local:44404` |
| `https://localhost:44404/nope` | 404, Site A's page (no domain matches) | URL `/nope`, domain empty |

The port is part of the domain name Umbraco stores. Add the port to every domain of a site that does not run on 80 or
443, otherwise the domain does not match and links to the site are rendered without the port.

## One site in several languages

One site with the domains `localhost:44403/en`, `/nl` and `/fr`, and the languages en-US (default), nl-NL and fr-FR.

| Request | Answer | Tracked as |
|---|---|---|
| `/en/nope` | 404, "Page not found" | domain `localhost:44403/en`, culture `en-US` |
| `/nl/nope` | 404, "Pagina niet gevonden" | domain `localhost:44403/nl`, culture `nl-NL` |
| `/fr/nope` | 404, "Page introuvable" | domain `localhost:44403/fr`, culture `fr-FR` |
| `/nl/missing.png` | 404, the Dutch page | a file, culture `nl-NL` |
| `/nope` | 404, the English page (default language) | domain and culture empty |

The 404 page is a page that varies by language, with a published variant in each language.

## Related

- [How it works](how-it-works.md) for the order in which blocking, redirects and the 404 page run.
- [Troubleshooting](troubleshooting.md) if the wrong 404 page is shown.
