# Dashboard actions

Both dashboards are lists whose rows have **entity actions**: the buttons on the right. You can add your own with
Umbraco's normal `entityAction` extension type.

## The entity types

| Rows | Entity type |
|---|---|
| Tracked URLs (**Page not found**) | `page-not-found-tracker-url` |
| Block rules (**Blocked URLs**) | `page-not-found-tracker-block-rule` |

Umbraco 17 has no entity type manifest. These strings are the registration: an entity action lists them in
`forEntityTypes`.

## How the actions are shown

- A row with **one** permitted action shows it as a button.
- A row with **two or more** shows the action with the highest `weight` as a button, and all of them in a **...** menu.
- The package's own actions: **Create redirect** (weight 0) and **Block** (weight -10) on tracked URLs, **Unblock** on
  block rules. An action with a negative weight below -10 goes in the menu; a positive weight makes yours the button.

## Example

The manifest registers one action for both kinds of rows:

<!-- sample: src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/samples/row-action.manifests.ts#row-action-manifest -->
```typescript
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'entityAction',
    kind: 'default',
    alias: 'My.EntityAction.Report',
    name: 'My report action',
    weight: -20, // below the package's own actions
    // The rows of the two dashboards: tracked URLs and block rules.
    forEntityTypes: ['page-not-found-tracker-url', 'page-not-found-tracker-block-rule'],
    api: () => import('./row-action.js'),
    meta: {
      icon: 'icon-info',
      label: 'Report',
    },
  },
];
```

The action is a class that extends `UmbEntityActionBase`:

<!-- sample: src/code/Umbraco.Community.PageNotFoundTracker.BackOffice.Client/samples/row-action.ts#row-action-class -->
```typescript
export class MyReportEntityAction extends UmbEntityActionBase<never> {
  override async execute(): Promise<void> {
    // The args carry the entity type and the row's unique (its id as a string), nothing else of the row.
    console.info(`Report requested for ${this.args.entityType} ${this.args.unique}`);
  }
}

export { MyReportEntityAction as api };
```

Register the manifest like any backoffice extension (see
[Umbraco's documentation on extensions](https://docs.umbraco.com/umbraco-cms/customizing/extending-overview)).

## What the action gets

The action's `args` carry the `entityType` and the `unique` of the row: its id, as a string. It does not get the other
fields of the row. To read them, ask your own backend, or the package's list endpoint
`GET /umbraco/pagenotfoundtracker/api/v1/tracked-urls` (and `.../block-rules`), which needs a backoffice user token. The
package's own actions read the row from the list's internal context, and that context is not available to your code.

After your action changes something, reload the page, or let the editor do it: the list does not refresh by itself.

## Who may use the dashboards

The dashboards, and the endpoints behind them, are open to the user groups of the `DashboardUserGroups` setting (see
[Configuration](../configuration.md)). When a list of groups is not enough, replace `IDashboardAccess`
(namespace `Umbraco.Community.PageNotFoundTracker.Core.Dashboard`), which has one method:
`bool IsAllowed(IEnumerable<string> userGroupAliases)`.

Register it from a composer marked `[ComposeAfter(typeof(PageNotFoundTrackerComposer))]`, with `AddUnique`
(see [the registration rule](README.md#the-registration-rule)). The `admin` rule of the default implementation is not
carried over: decide yourself whether administrators always pass.

## What is guaranteed, and what is not

- The entity type strings and the `entityAction` registration are stable.
- The dashboards' collection elements, their context, the data marks and the dialog elements are internal. Do not
  depend on them.
- The visibility of the package's own actions (for example **Create redirect** only on page rows) is theirs. Your action
  appears on every row of the entity types you list, unless you add a condition to the manifest.
