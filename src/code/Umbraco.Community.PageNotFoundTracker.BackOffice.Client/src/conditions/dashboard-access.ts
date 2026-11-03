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

/** Whether an access response is a successful answer (`allowed` true or false), as opposed to a failure. */
function isSuccess(result: PnftDashboardAccessResult | undefined): boolean {
  return !result?.error && typeof result?.data?.allowed === 'boolean';
}

/**
 * Wraps the access request so a successful answer is requested once: the first call starts the request and
 * concurrent calls share it. A successful answer (also `allowed: false`) is cached for every later call. A failure
 * (rejected or throwing request, error response, missing body) counts as not permitted (fail closed) but is not
 * cached, so the next call asks again rather than hiding the dashboard for the whole page load.
 * Nothing is requested until the returned function is called.
 */
export function createDashboardAccessCheck(request: () => Promise<PnftDashboardAccessResult | undefined>): () => Promise<boolean> {
  let pending: Promise<boolean> | undefined;

  return () => {
    if (pending) {
      return pending;
    }

    const attempt: Promise<boolean> = Promise.resolve()
      .then(request)
      .then(
        (result) => {
          if (!isSuccess(result)) {
            pending = undefined;
          }

          return isAccessAllowed(result);
        },
        () => {
          pending = undefined;
          return false;
        },
      );
    pending = attempt;

    return attempt;
  };
}
