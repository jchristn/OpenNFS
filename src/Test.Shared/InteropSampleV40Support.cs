namespace Test.Shared
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;

    internal static class InteropSampleV40Support
    {
        internal static async Task<byte[]> ResolveSampleExportRootV40Async(
            OpenNfsClient client,
            CancellationToken cancellationToken)
        {
            OpenNfsV40LookupResult rootResult = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
            if (!rootResult.IsSuccess || rootResult.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected NFSv4.0 PUTROOTFH against the sample artifact to return a usable pseudo-root filehandle.");
            }

            OpenNfsV40ReadDirectoryResult rootListing = await client.Directories.ReadDirectoryV40Async(
                rootResult.ObjectFileHandle.ToArray(),
                0UL,
                new byte[8],
                4096U,
                cancellationToken).ConfigureAwait(false);
            if (!rootListing.IsSuccess)
            {
                throw new InvalidOperationException("Expected NFSv4.0 READDIR against the sample artifact root to succeed.");
            }

            bool rootAlreadyLooksLikeExport = rootListing.Entries.Any(
                static entry => string.Equals(entry.Name, "docs", StringComparison.Ordinal)
                    || string.Equals(entry.Name, "hello.txt", StringComparison.Ordinal));
            if (rootAlreadyLooksLikeExport)
            {
                return rootResult.ObjectFileHandle.ToArray();
            }

            OpenNfsV40LookupResult exportsLookup = await client.Directories.LookupV40Async(
                rootResult.ObjectFileHandle.ToArray(),
                "exports",
                cancellationToken).ConfigureAwait(false);
            if (!exportsLookup.IsSuccess || exportsLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample NFSv4.0 pseudo-root to expose an 'exports' directory.");
            }

            OpenNfsV40LookupResult sampleLookup = await client.Directories.LookupV40Async(
                exportsLookup.ObjectFileHandle.ToArray(),
                "sample",
                cancellationToken).ConfigureAwait(false);
            if (!sampleLookup.IsSuccess || sampleLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample NFSv4.0 export tree to expose an 'exports/sample' directory.");
            }

            return sampleLookup.ObjectFileHandle.ToArray();
        }
    }
}
