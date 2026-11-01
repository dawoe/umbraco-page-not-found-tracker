// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Tests.Integration.Testing;

/// <summary>
/// Prepares and tears down the SQLite test databases once for the whole test run, as the Umbraco integration testing
/// documentation prescribes. It must stay in the global namespace so NUnit applies it to every fixture in the assembly.
/// </summary>
[SetUpFixture]
internal sealed class GlobalSetup
{
    private GlobalSetupTeardown? setupTeardown;

    /// <summary>
    /// Prepares the test databases.
    /// </summary>
    [OneTimeSetUp]
    public void SetUp()
    {
        this.setupTeardown = new GlobalSetupTeardown();
        this.setupTeardown.SetUp();
    }

    /// <summary>
    /// Removes the test databases.
    /// </summary>
    [OneTimeTearDown]
    public void TearDown() => this.setupTeardown?.TearDown();
}
