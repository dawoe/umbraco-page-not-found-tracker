// Pure helper of the "Delete all matching" dialog. No backoffice runtime imports, so it runs in Vitest.

/** The word the editor must type before an unfiltered "delete all" is confirmed. */
export const PNFT_DELETE_ALL_WORD = 'delete';

/** Whether the typed text confirms deleting everything: the word `delete`, ignoring case and surrounding spaces. */
export function isDeleteAllConfirmed(input: string): boolean {
  return input.trim().toLowerCase() === PNFT_DELETE_ALL_WORD;
}
