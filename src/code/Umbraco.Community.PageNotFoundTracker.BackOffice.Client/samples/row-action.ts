import { UmbEntityActionBase } from '@umbraco-cms/backoffice/entity-action';

// docs:begin row-action-class
export class MyReportEntityAction extends UmbEntityActionBase<never> {
  override async execute(): Promise<void> {
    // The args carry the entity type and the row's unique (its id as a string), nothing else of the row.
    console.info(`Report requested for ${this.args.entityType} ${this.args.unique}`);
  }
}

export { MyReportEntityAction as api };
// docs:end row-action-class
