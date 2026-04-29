namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs the full shared Touchstone suite catalog as a single NUnit test.
    /// </summary>
    [TestFixture]
    public sealed class OpenNfsNunitFactTests : TouchstoneNunitBase
    {
        /// <summary>
        /// Gets the shared suite catalog used by this test class.
        /// </summary>
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get
            {
                return OpenNfsSuites.All;
            }
        }

        /// <summary>
        /// Executes every non-skipped descriptor in the shared suite catalog.
        /// </summary>
        /// <returns>A task that completes when the suite catalog has finished running.</returns>
        [Test]
        public async Task RunAll()
        {
            CancellationToken cancellationToken = CancellationToken.None;
            await RunAllAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

