namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using global::Xunit;

    /// <summary>
    /// Exposes each fast shared Touchstone adapter-smoke descriptor as an individual xUnit theory row.
    /// The full heavy matrix runs through Test.Automated instead of this adapter project.
    /// </summary>
    public sealed class OpenNfsTheoryTests
    {
        /// <summary>
        /// Gets one theory row for each non-skipped adapter-smoke descriptor.
        /// </summary>
        /// <returns>The theory data for the fast adapter-smoke suite catalog.</returns>
        public static TouchstoneTheoryData TestCases()
        {
            return new TouchstoneTheoryData(OpenNfsSuites.AdapterSmoke);
        }

        /// <summary>
        /// Executes a single Touchstone descriptor.
        /// </summary>
        /// <param name="testCase">Descriptor to execute.</param>
        /// <returns>A task that completes when the descriptor finishes running.</returns>
        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            CancellationToken cancellationToken = CancellationToken.None;
            await testCase.ExecuteAsync(cancellationToken);
        }
    }
}
