/** The extension type of a redirect form (the form the create redirect modal hosts). */
export const PNFT_REDIRECT_FORM_EXTENSION_TYPE = 'pnftRedirectForm';

/** The alias of the package's default redirect form (a content picker for the target page). */
export const PNFT_DEFAULT_REDIRECT_FORM_ALIAS = 'Pnft.RedirectForm.Default';

/**
 * The weight of the default redirect form. The modal hosts the registered form with the highest weight; the default is
 * below 0, so a form registered without a weight (0) already replaces it.
 */
export const PNFT_DEFAULT_REDIRECT_FORM_WEIGHT = -100;

/** The alias of the create redirect modal. */
export const PNFT_CREATE_REDIRECT_MODAL_ALIAS = 'Pnft.Modal.CreateRedirect';

/** The alias of the create redirect entity action on tracked URL rows. */
export const PNFT_CREATE_REDIRECT_ACTION_ALIAS = 'Pnft.EntityAction.TrackedUrl.CreateRedirect';

/** The alias of the condition that permits the create redirect action only on rows the server flagged. */
export const PNFT_CAN_CREATE_REDIRECT_CONDITION_ALIAS = 'Pnft.Condition.CanCreateRedirect';
