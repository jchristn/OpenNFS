namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Exposes each fast shared Touchstone adapter-smoke descriptor as an individual NUnit test case.
    /// The full heavy matrix runs through Test.Automated instead of this adapter project.
    /// </summary>
    [TestFixture]
    public sealed class OpenNfsNunitTests
    {
        /// <summary>
        /// Gets one NUnit test case for each non-skipped adapter-smoke descriptor.
        /// </summary>
        /// <returns>The fast adapter-smoke test case source.</returns>
        public static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(OpenNfsSuites.AdapterSmoke);
        }

        /// <summary>
        /// Executes a single Touchstone descriptor.
        /// </summary>
        /// <param name="testCase">Descriptor to execute.</param>
        /// <returns>A task that completes when the descriptor finishes running.</returns>
        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            CancellationToken cancellationToken = CancellationToken.None;
            await testCase.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
