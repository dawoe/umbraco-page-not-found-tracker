import type { ManifestDashboard } from "@umbraco-cms/backoffice/dashboard";

export const manifests: Array<ManifestDashboard> = [
  {
    type: "dashboard",
    alias: "Umbraco.Community.PageNotFoundTracker.Dashboard.Placeholder",
    name: "Page Not Found Tracker Placeholder Dashboard",
    element: () => import("./placeholder-dashboard.element"),
    weight: 100,
    meta: {
      label: "Page Not Found Tracker",
      pathname: "page-not-found-tracker",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Settings",
      },
    ],
  },
];
