import type { UmbBackofficeExtensionRegistry } from "@umbraco-cms/backoffice/extension-registry";
import { manifests as blockingManifests } from "./blocking/manifests";
import { manifests as collectionManifests } from "./collection/manifests";
import { manifests as conditionManifests } from "./conditions/manifests";
import { manifests as trackedUrlsDashboardManifests } from "./dashboards/tracked-urls/manifests";
import { manifests as localizationManifests } from "./localization/manifests";
import { manifests as redirectManifests } from "./redirect/manifests";

export function registerManifest(registry: UmbBackofficeExtensionRegistry) {
  registry.registerMany([
    ...localizationManifests,
    ...conditionManifests,
    ...collectionManifests,
    ...trackedUrlsDashboardManifests,
    ...redirectManifests,
    ...blockingManifests,
  ]);
}
