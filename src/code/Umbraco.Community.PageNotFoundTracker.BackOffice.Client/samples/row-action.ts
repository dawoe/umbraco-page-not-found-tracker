import { UmbEntityActionBase } from '@umbraco-cms/backoffice/entity-action';

// docs:begin row-action-class
export class MyReportEntityAction extends UmbEntityActionBase<never> {
  override async execute(): Promise<void> {
    // The args carry the entity type and the row's unique (its id as a string). To read the other row fields, consume
    // the collection context of that dashboard, as the package's own Block action does.
    console.info(`Report requested for ${this.args.entityType} ${this.args.unique}`);
  }
}

export { MyReportEntityAction as api };
// docs:end row-action-class
