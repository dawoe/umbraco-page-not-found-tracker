# The dashboards

This page is for editors. The package adds two dashboards to the **Content** section of the backoffice:

- **Page not found** lists the URLs visitors asked for that do not exist.
- **Blocked URLs** lists the URLs you have blocked.

## Who can see them

By default users in the **Administrators** and **Editors** groups. Your implementer can change this with the
`DashboardUserGroups` setting (see [Configuration](configuration.md)). You also need access to the Content section.
If the tabs are missing, see [Troubleshooting](troubleshooting.md).

## Page not found

Every request that ends in a 404 is counted in the background. Each different URL is one row. A page, a file and the
same URL on another site or in another language are different rows (see [How it works](how-it-works.md)).

| Column | Meaning |
|---|---|
| URL | The address that was requested |
| Domain | The site (Umbraco domain) the request was for. Shown as *No domain* when no domain matched |
| Culture | The language of that domain. Shown as *No culture* when there is none |
| Type | **Page** or **File** |
| Hits | How many times it was requested |
| First seen, Last seen | When it was first and last requested |

The list is sorted on **Hits**, highest first. Click a column heading to sort on it, and click again to reverse.

- **Search** finds URLs that contain what you type. It ignores capitals, so `/Old-Page/` finds `/old-page`. It looks
  at the URL only, not at the domain or the culture.
- **Domain** and **Culture** filters limit the list to one value. **No domain** and **No culture** show the rows where
  that value is empty.
- **Items per page** is 20, 50 or 100. The total number of URLs is shown next to the list.

The list does **not** refresh by itself, and new 404s appear after the next write to the database (every minute by
default). Reload the page, or change a filter, to see the latest data.

URLs that nobody requested for a year disappear from the list on their own. Your implementer can change that period
or switch it off (see [Clean-up](how-it-works.md#clean-up)).

What you may see instead of a list:

| Message | Meaning |
|---|---|
| No 404s tracked yet | Nothing has been recorded yet. Request a page that does not exist, wait a minute and reload |
| No matching URLs | The search or the filters match nothing. Change or clear them |
| The tracked URLs could not be loaded | The request failed. Check that the site is running and reload |
| Tracking database unsupported or unavailable | The package cannot use the database. Your implementer has to fix it and restart the site |

### What you can do with a row

- **Create redirect** sends visitors of the URL to a page you choose, and removes the row. Only available for pages
  without a query string. See [Redirects](redirects.md).
- **Block** stops the URL for good with an answer such as *410 Gone*, and removes the row. Look for it in the
  **...** menu next to **Create redirect**. For a file row it is shown as a button. See [Blocking](blocking.md).
- **Delete** removes the row from the list after a confirmation. It is in the **...** menu. See
  [Deleting URLs](#deleting-urls).

### Deleting URLs

Delete a URL you do not want to see any more, for example a row you already handled another way.

- **Delete** (in the **...** menu of a row) deletes that one row.
- **Delete selected**: tick the boxes in front of the rows (the box in the header selects the whole page) and click
  **Delete selected** at the bottom of the list. Only the rows on the current page can be selected, so one go deletes at
  most 100.
- **Delete all matching** (top right) deletes every URL the current search and filters match, not only the ones on
  this page. The dialog tells you how many that is. When no search or filter is set it deletes **all** tracked URLs, and
  you have to type `delete` before the **Delete** button works.

Deleting cannot be undone, and each of these asks for a confirmation first. A deleted URL is not blocked or redirected:
when it is requested again it is tracked as a new row, starting from one hit. Deleting is open to the same users as the
rest of the dashboard. How a delete interacts with the hits that are still waiting to be written is explained in
[How it works](how-it-works.md#deleting-tracked-urls).

## Blocked URLs

| Column | Meaning |
|---|---|
| Pattern | The URL pattern that is blocked, for example `/wp-admin/*` |
| Status code | The answer visitors get: 403, 404 or 410 |
| Domain, Culture | Where the rule applies. *All domains* and *All cultures* mean the rule has no limit |
| Created | When the rule was added |

The newest rule is on top. You can search patterns, filter on domain and culture, sort on any column and choose how
many rules are shown per page, the same way as in the other list.

- **Add rule** (top right) opens the dialog to add a pattern by hand.
- **Edit** (the button on a row) opens the rule for changes (see [Blocking](blocking.md#edit-a-rule)).
- **Unblock** (in the **...** menu next to it) removes the rule after a confirmation. The URLs are answered normally
  again, and tracked again from then on.

How patterns work is explained in [Blocking](blocking.md).
