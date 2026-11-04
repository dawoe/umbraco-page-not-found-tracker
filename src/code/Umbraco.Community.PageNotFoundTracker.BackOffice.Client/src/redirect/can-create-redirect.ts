// Pure rule of the create redirect action's visibility. No backoffice runtime imports, so it runs in Vitest.

/** The part of a collection row the rule reads. */
export interface PnftRedirectCandidate {
  unique: string;
  canCreateRedirect: boolean;
}

/**
 * Whether the row with this `unique` may show the create redirect action: only when the server flagged it
 * (`canCreateRedirect`, the active provider's answer). An unknown row or a missing unique is not permitted.
 */
export function canCreateRedirectFor(
  rows: ReadonlyArray<PnftRedirectCandidate> | undefined,
  unique: string | null | undefined,
): boolean {
  if (!rows || unique === null || unique === undefined) {
    return false;
  }

  return rows.find((row) => row.unique === unique)?.canCreateRedirect === true;
}
