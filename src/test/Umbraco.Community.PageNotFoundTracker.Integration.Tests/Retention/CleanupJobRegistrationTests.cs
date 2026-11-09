// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Models;
using Umbraco.Cms.Infrastructure.Persistence.Repositories;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Infrastructure.Services;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Community.PageNotFoundTracker.Infrastructure.Extensions;

namespace Umbraco.Community.PageNotFoundTracker.Integration.Tests.Retention;

/// <summary>
/// Tests that Umbraco picks up the clean-up job as a distributed job and stores its row, as it does on the startup of a
/// site that installs or upgrades the package.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
internal sealed class CleanupJobRegistrationTests : UmbracoIntegrationTest
{
    private const string JobName = "PageNotFoundTracker.TrackedUrlCleanup";

    /// <inheritdoc />
    /// <remarks>This test base does not run package composers, so the package's registrations are added here.</remarks>
    protected override void CustomTestSetup(IUmbracoBuilder builder) => builder.AddPageNotFoundTracker();

    /// <summary>
    /// The job is among Umbraco's distributed jobs and gets its row in <c>umbracoDistributedJob</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Cleanup_job_is_registered_with_umbraco()
    {
        IEnumerable<IDistributedBackgroundJob> jobs = this.GetRequiredService<IEnumerable<IDistributedBackgroundJob>>();

        await this.GetRequiredService<IDistributedJobService>().EnsureJobsAsync();

        DistributedBackgroundJobModel? row;
        using (IScope scope = this.GetRequiredService<IScopeProvider>().CreateScope(autoComplete: true))
        {
            row = this.GetRequiredService<IDistributedJobRepository>().GetByName(JobName);
        }

        Assert.Multiple(() =>
        {
            Assert.That(jobs.Select(job => job.Name), Does.Contain(JobName));
            Assert.That(row, Is.Not.Null, "Umbraco stored no row for the clean-up job");
            Assert.That(row?.Period, Is.EqualTo(TimeSpan.FromDays(1)));
        });
    }
}
