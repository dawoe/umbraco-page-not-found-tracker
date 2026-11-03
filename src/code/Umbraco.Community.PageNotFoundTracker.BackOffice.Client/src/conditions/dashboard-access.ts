/** The shape of an access response as the generated client (through `tryExecute`) returns it. */
export interface PnftDashboardAccessResult {
  data?: { allowed: boolean };
  error?: unknown;
}

/**
 * Whether an access response permits the dashboard. Fails closed: an error, a missing body or anything but
 * `allowed: true` means not permitted.
 */
export function isAccessAllowed(result: PnftDashboardAccessResult | undefined): boolean {
  return !result?.error && result?.data?.allowed === true;
}

/**
 * Wraps the access request so it runs at most once: the first call starts it, every call shares the cached
 * promise. A rejected or throwing request counts as not permitted and is not retried (fail closed).
 * Nothing is requested until the returned function is called.
 */
export function createDashboardAccessCheck(request: () => Promise<PnftDashboardAccessResult | undefined>): () => Promise<boolean> {
  let pending: Promise<boolean> | undefined;

  return () => {
    pending ??= Promise.resolve()
      .then(request)
      .then(isAccessAllowed, () => false);

    return pending;
  };
}
