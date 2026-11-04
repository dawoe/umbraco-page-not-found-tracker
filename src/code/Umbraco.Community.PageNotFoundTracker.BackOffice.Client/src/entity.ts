/** The entity type of a tracked 404 URL (the rows of the dashboard collection). */
// Umbraco 17 has no entityType manifest: this constant is the registration; entity actions target it with forEntityTypes.
export const PNFT_TRACKED_URL_ENTITY_TYPE = 'page-not-found-tracker-url';

export type PnftTrackedUrlEntityType = typeof PNFT_TRACKED_URL_ENTITY_TYPE;
