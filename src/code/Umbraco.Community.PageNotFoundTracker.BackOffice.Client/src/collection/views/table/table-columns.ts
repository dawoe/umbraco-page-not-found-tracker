// The column model of the package's tables and their sort mapping. Pure: no backoffice runtime imports.

/** One data column of a package table. */
export interface PnftTableColumnDefinition<TAlias extends string = string, TSortColumn extends string = string> {
  alias: TAlias;
  /** The localization key of the header. */
  labelKey: string;
  /** The `data-mark` of the header. */
  sortMark: string;
  /** The API sort column. */
  sortColumn: TSortColumn;
  align?: 'left' | 'center' | 'right';
}

/** The alias of the entity actions column (no header, no sorting). */
export const PNFT_ENTITY_ACTIONS_COLUMN = 'entityActions';

/** The API sort column of a table column alias, undefined for a column that does not sort. */
export function sortColumnIn<TAlias extends string, TSortColumn extends string>(
  columns: ReadonlyArray<PnftTableColumnDefinition<TAlias, TSortColumn>>,
  alias: string,
): TSortColumn | undefined {
  return columns.find((column) => column.alias === alias)?.sortColumn;
}

/** The table column alias of an API sort column. */
export function columnAliasIn<TAlias extends string, TSortColumn extends string>(
  columns: ReadonlyArray<PnftTableColumnDefinition<TAlias, TSortColumn>>,
  sortColumn: TSortColumn | undefined,
): TAlias | undefined {
  return columns.find((column) => column.sortColumn === sortColumn)?.alias;
}

/** Renders a UTC ISO 8601 timestamp in the user's locale and time zone; invalid or missing gives ''. */
export function formatLocalDateTime(iso: string | undefined): string {
  if (!iso) {
    return '';
  }

  const date = new Date(iso);

  return Number.isNaN(date.getTime()) ? '' : date.toLocaleString();
}
