import { PNFT_TRACKED_URL_ENTITY_TYPE } from '../entity.js';
import {
  PNFT_CAN_CREATE_REDIRECT_CONDITION_ALIAS,
  PNFT_CREATE_REDIRECT_ACTION_ALIAS,
  PNFT_CREATE_REDIRECT_MODAL_ALIAS,
  PNFT_DEFAULT_REDIRECT_FORM_ALIAS,
  PNFT_DEFAULT_REDIRECT_FORM_WEIGHT,
} from './constants.js';

/**
 * Redirect creation: the row action (shown only on rows the server flagged), its visibility condition, the modal and
 * the default redirect form. Implementers replace the form by registering a `pnftRedirectForm` with a higher weight.
 */
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'condition',
    alias: PNFT_CAN_CREATE_REDIRECT_CONDITION_ALIAS,
    name: 'Page Not Found Tracker Can Create Redirect Condition',
    api: () => import('./can-create-redirect.condition.js'),
  },
  {
    type: 'entityAction',
    kind: 'default',
    alias: PNFT_CREATE_REDIRECT_ACTION_ALIAS,
    name: 'Page Not Found Tracker Create Redirect Entity Action',
    forEntityTypes: [PNFT_TRACKED_URL_ENTITY_TYPE],
    api: () => import('./create-redirect.action.js'),
    meta: {
      icon: 'icon-arrow-right',
      label: '#pageNotFoundTracker_createRedirect',
      additionalOptions: true,
    },
    conditions: [{ alias: PNFT_CAN_CREATE_REDIRECT_CONDITION_ALIAS }],
  },
  {
    type: 'modal',
    alias: PNFT_CREATE_REDIRECT_MODAL_ALIAS,
    name: 'Page Not Found Tracker Create Redirect Modal',
    element: () => import('./redirect-modal.element.js'),
  },
  {
    type: 'pnftRedirectForm',
    alias: PNFT_DEFAULT_REDIRECT_FORM_ALIAS,
    name: 'Page Not Found Tracker Default Redirect Form',
    weight: PNFT_DEFAULT_REDIRECT_FORM_WEIGHT,
    element: () => import('./default-form/default-redirect-form.element.js'),
  },
];
