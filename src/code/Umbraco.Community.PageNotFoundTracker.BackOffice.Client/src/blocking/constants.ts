/** The entity type of a block rule (the rows of the blocked URLs collection). */
// Umbraco 17 has no entityType manifest: this constant is the registration; entity actions target it with forEntityTypes.
export const PNFT_BLOCK_RULE_ENTITY_TYPE = 'page-not-found-tracker-block-rule';

export type PnftBlockRuleEntityType = typeof PNFT_BLOCK_RULE_ENTITY_TYPE;

/** The alias of the block rule collection. */
export const PNFT_BLOCK_RULE_COLLECTION_ALIAS = 'Pnft.Collection.BlockRule';

/** The alias of the block rule collection repository. */
export const PNFT_BLOCK_RULE_REPOSITORY_ALIAS = 'Pnft.Repository.BlockRule';

/** The alias of the table view of the block rule collection (its only view). */
export const PNFT_BLOCK_RULE_TABLE_VIEW_ALIAS = 'Pnft.CollectionView.BlockRule.Table';

/** The alias of the pattern search box (a `collectionTextFilter`). */
export const PNFT_BLOCK_RULE_TEXT_FILTER_ALIAS = 'Pnft.CollectionTextFilter.BlockRule';

/** The alias of the blocked URLs dashboard. */
export const PNFT_BLOCK_RULES_DASHBOARD_ALIAS = 'Umbraco.Community.PageNotFoundTracker.Dashboard.BlockRules';

/** The pathname of the blocked URLs dashboard (`/umbraco/section/content/dashboard/blocked-urls`). */
export const PNFT_BLOCK_RULES_DASHBOARD_PATHNAME = 'blocked-urls';

/**
 * The weight of the blocked URLs dashboard. Umbraco sorts dashboard tabs by weight, descending, so 4 puts it right after
 * the tracked URLs dashboard (weight 5); the plan's 6 placed it before (seen in 17.8).
 */
export const PNFT_BLOCK_RULES_DASHBOARD_WEIGHT = 4;

/** The alias of the block action on tracked URL rows. */
export const PNFT_BLOCK_ACTION_ALIAS = 'Pnft.EntityAction.TrackedUrl.Block';

/** The alias of the unblock action on block rule rows. */
export const PNFT_UNBLOCK_ACTION_ALIAS = 'Pnft.EntityAction.BlockRule.Unblock';

/** The alias of the edit action on block rule rows. */
export const PNFT_EDIT_RULE_ACTION_ALIAS = 'Pnft.EntityAction.BlockRule.Edit';

/** The alias of the "Add rule" collection action. */
export const PNFT_ADD_BLOCK_RULE_ACTION_ALIAS = 'Pnft.CollectionAction.BlockRule.Add';

/** The alias of the create block rule modal. */
export const PNFT_CREATE_BLOCK_RULE_MODAL_ALIAS = 'Pnft.Modal.CreateBlockRule';
