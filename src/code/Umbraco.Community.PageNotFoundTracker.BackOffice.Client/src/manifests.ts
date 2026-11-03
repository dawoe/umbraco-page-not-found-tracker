import type { UmbBackofficeExtensionRegistry } from "@umbraco-cms/backoffice/extension-registry";
import { manifests as placeholderDashboardManifests } from "./dashboards/placeholder/manifests";
import { manifests as localizationManifests } from "./localization/manifests";

export function registerManifest(registry: UmbBackofficeExtensionRegistry) {
  registry.registerMany([...localizationManifests, ...placeholderDashboardManifests]);
}
