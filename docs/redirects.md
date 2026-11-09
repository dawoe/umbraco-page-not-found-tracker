# Redirects

A tracked URL that visitors keep asking for can be turned into a redirect to a page that exists.

## Create a redirect

1. Open **Content**, then the **Page not found** dashboard.
2. Click **Create redirect** on the row.
3. In **Redirect to**, pick the page visitors should be sent to. The page is required.
4. Click **Create redirect**.

The dashboard reloads, the row is gone, and a message confirms the redirect. From now on a request for that URL
is answered with a `301` to the chosen page. A query string the visitor adds is passed on to the page.

The redirect is an ordinary Umbraco redirect. It shows up in Umbraco's **Redirect URL Management** dashboard in the
Content section, where you can remove it again. If a removed redirect's URL is requested later, it is tracked again
as a new row.

## When the action is not available

- **Files.** Only page rows can get a redirect. A `.css` or `.pdf` row only has **Block**.
- **Rows with a query string.** With the setting `IncludeQueryString` on, a row can contain a query string, and
  Umbraco's redirects cannot match one. Such a row has no **Create redirect**.

## When a redirect is refused

The dialog shows the reason and nothing is created. The most common one: **the target page must be published**. A page
that has several languages must also be published in the language of the row.

## Good to know

- **Sites and languages.** The redirect is registered for the site (domain) and language of the row. `/old` on Site A
  and `/old` on Site B are different rows and can point to different pages. See
  [Multi-site and languages](multi-site-and-language.md).
- **An existing redirect is replaced.** Creating a redirect for a URL that already has one for the same site and
  language replaces it.
- **Redirects always apply.** Umbraco has no setting that switches off applying redirects. The setting
  `Umbraco:CMS:WebRouting:DisableRedirectUrlTracking` only stops Umbraco from creating redirects itself when a page is
  renamed or moved.
- **A block rule wins.** A request that matches a [block rule](blocking.md) never reaches the redirect.

## Different redirects

Implementers can replace the redirect backend (for example to write to a redirect system of their own) and the form
in the dialog: see [Redirect providers](extending/redirect-providers.md).

If a redirect was created but does not work, see [Troubleshooting](troubleshooting.md).
