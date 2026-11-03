import type { UmbEntryPointOnInit } from '@umbraco-cms/backoffice/extension-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { client } from './api/client.gen.js';
import { registerManifest } from './manifests';

export const onInit: UmbEntryPointOnInit = (host, extensionRegistry) => {
  registerManifest(extensionRegistry);

  // Configure the generated Management API client from Umbraco's auth context: server URL,
  // credentials, bearer token (with refresh) and Umbraco's response interceptors. No base URL is
  // baked into the bundle.
  host.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
    authContext?.configureClient(client);
  });
};
