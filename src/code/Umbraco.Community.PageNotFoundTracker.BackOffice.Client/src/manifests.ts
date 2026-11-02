import type { UmbBackofficeExtensionRegistry } from "@umbraco-cms/backoffice/extension-registry";
import { manifests as placeholderDashboardManifests } from "./dashboards/placeholder/manifests";

export function registerManifest(registry: UmbBackofficeExtensionRegistry) {
  registry.registerMany([...placeholderDashboardManifests]);
}
