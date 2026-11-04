import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbDefaultCollectionContext, type UmbCollectionConfiguration } from '@umbraco-cms/backoffice/collection';
import { UmbBasicState } from '@umbraco-cms/backoffice/observable-api';
import { PNFT_TRACKED_URL_TABLE_VIEW_ALIAS } from './constants.js';
import type { PnftTrackedUrlRepository } from './repository/tracked-url.repository.js';
import { PNFT_DEFAULT_PAGE_SIZE, classifyError, parsePageSize, withPageReset, type PnftCollectionErrorKind } from './state.js';
import type {
  PnftTrackedUrlCollectionFilterModel,
  PnftTrackedUrlCollectionModel,
  PnftTrackedUrlFilterOptionsModel,
} from './types.js';

/**
 * The context of the tracked URL collection: Umbraco's default collection context (repository, items, total,
 * pagination, filter, ordering, selection, views) plus what it cannot carry:
 *
 * - the error of the last list request (`error`: `error`, or `unavailable` for a 503), which the default
 *   context drops;
 * - search, filter, sort and page size changes resetting to page 1 (the default keeps `skip`);
 * - the page size choice (20, 50, 100; default 20 instead of the framework's 50);
 * - the filter choices from the repository.
 *
 * Nothing is requested at construction: the collection element loads the list when it is rendered.
 *
 * The overrides mirror Umbraco 17.8 behaviour (`collection-default.context.js`, incl. its protected members);
 * re-check them on every Umbraco upgrade.
 */
export class PnftTrackedUrlCollectionContext extends UmbDefaultCollectionContext<
  PnftTrackedUrlCollectionModel,
  PnftTrackedUrlCollectionFilterModel
> {
  #error = new UmbBasicState<PnftCollectionErrorKind | undefined>(undefined);

  /** The error of the last list request, undefined when it succeeded. */
  readonly error = this.#error.asObservable();

  #requestNumber = 0;

  constructor(host: UmbControllerHost) {
    super(host, PNFT_TRACKED_URL_TABLE_VIEW_ALIAS, { orderBy: 'Hits', orderDirection: 'Descending' });
    this.setConfig({});
  }

  /** Sets the configuration; the page size defaults to 20 unless the configuration names one. */
  override setConfig(config: UmbCollectionConfiguration): void {
    super.setConfig({ ...config, pageSize: parsePageSize(config.pageSize ?? PNFT_DEFAULT_PAGE_SIZE) });
  }

  /** Merges a filter change and reloads; a change that does not set `skip` goes back to page 1. */
  override setFilter(filter: Partial<PnftTrackedUrlCollectionFilterModel>): void {
    const change = withPageReset(filter);

    if (change.skip === 0 && this.pagination.getCurrentPageNumber() !== 1) {
      // Move the pager first: its change event sets skip 0 and drops the remembered page from the interaction
      // memory. Done afterwards, the order or filter memory written by the change below would restore the
      // remembered page (the framework applies the memories while it is still writing them).
      this.pagination.setCurrentPageNumber(1);
    }

    super.setFilter(change);
  }

  /** Changes the page size (20, 50 or 100) and goes back to page 1. */
  setPageSize(size: number): void {
    const pageSize = parsePageSize(size);

    this.pagination.setPageSize(pageSize);
    this.setFilter({ take: pageSize });
  }

  /** Requests the choices of the domain and culture filters (once the repository is there). */
  async requestFilterOptions(): Promise<PnftTrackedUrlFilterOptionsModel | undefined> {
    await this._init;
    const repository = this._repository as PnftTrackedUrlRepository | undefined;
    const { data } = (await repository?.requestFilterOptions()) ?? {};

    return data;
  }

  /**
   * Requests the current page. Same as the default context, but it keeps the error (and clears the rows
   * on an error, so the error state shows instead of stale rows) and ignores a response that a newer
   * request has overtaken.
   */
  protected override async _requestCollection(): Promise<void> {
    await this._init;

    if (!this._configured) {
      this._configure();
    }

    if (!this._repository) {
      throw new Error(`Missing repository for ${this.manifest?.alias}`);
    }

    const requestNumber = ++this.#requestNumber;
    this._loading.setValue(true);

    const { data, error } = await this._repository.requestCollection(this._filter.getValue());

    if (requestNumber !== this.#requestNumber) {
      return;
    }

    const errorKind = classifyError(error);

    if (data) {
      this.#error.setValue(undefined);
      this._items.setValue(data.items as Array<PnftTrackedUrlCollectionModel>);
      this._setTotalItems(data.total);
    } else if (errorKind) {
      this.#error.setValue(errorKind);
      this._items.setValue([]);
      this._setTotalItems(0);
    }

    this._loading.setValue(false);
  }
}

export { PnftTrackedUrlCollectionContext as api };
