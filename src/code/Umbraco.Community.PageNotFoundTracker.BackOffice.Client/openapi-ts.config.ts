import { defineConfig } from '@hey-api/openapi-ts';

// Generates the typed client for the package's Management API into src/api (committed).
// Run `npm run generate-client` while the test site runs on the single-site profile, or point
// OPENAPI_URL at another running site. For the local self-signed dev certificate set
// NODE_TLS_REJECT_UNAUTHORIZED=0 in the shell (local development only, never anywhere real).
// The base URL is NOT baked into the client: entry.ts configures it at runtime from Umbraco's
// auth context.
const input =
  process.env.OPENAPI_URL ??
  'https://localhost:44401/umbraco/swagger/page-not-found-tracker-management-api/swagger.json';

export default defineConfig({
  input,
  output: {
    path: 'src/api',
  },
  parser: {
    patch: {
      // The document has no `servers`, so the generator would infer the origin it was fetched from
      // (a dev-machine URL) into the generated types. Declare a relative server instead.
      input: (spec) => {
        if ('openapi' in spec) {
          spec.servers = [{ url: '/' }];
        }
      },
    },
  },
  plugins: [
    {
      name: '@hey-api/client-fetch',
      // No baseUrl from the OpenAPI document: the server URL comes from UMB_AUTH_CONTEXT at runtime.
      // Errors reject, so Umbraco's tryExecute can map them and show the problem details.
      baseUrl: false,
      throwOnError: true,
    },
    '@hey-api/typescript',
    '@hey-api/sdk',
  ],
});
