let markConfigured: () => void;

/**
 * Resolves once entry.ts has configured the generated client from Umbraco's auth context. Code that requests
 * once and caches the answer (the dashboard access condition) waits for it, so it never sends a request
 * without the server URL and token.
 */
export const whenClientConfigured = new Promise<void>((resolve) => {
  markConfigured = resolve;
});

/** Called by entry.ts after `configureClient`. */
export function markClientConfigured(): void {
  markConfigured();
}
