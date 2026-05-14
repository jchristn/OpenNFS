namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Grouped client typed reply-decoding suites.
    /// </summary>
    internal static class ClientGroupedFoundationDecodeCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "ClientGroupedSuites",
                    caseId: "FileAndDirectoryApisDecodeTypedReplies",
                    displayName: "Grouped file and directory APIs decode typed NFSv3 replies without requiring an open client",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        ClientGroupedFoundationDecodeSupport.VerifyTypedReplyDecoding();
                        return Task.CompletedTask;
                    }),
            };
        }
    }
}
