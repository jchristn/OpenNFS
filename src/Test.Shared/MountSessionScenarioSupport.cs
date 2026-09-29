namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;

    /// <summary>
    /// Describes one NFSv3 server the mounted-session scenario matrix runs against.
    /// </summary>
    internal sealed class MountSessionScenarioTarget
    {
        internal MountSessionScenarioTarget(
            string name,
            OpenNfsClient client,
            OpenNfsMountSession session,
            bool modeRoundTripsExactly,
            bool rejectsWindowsUnsafeNames = false)
        {
            Name = name;
            Client = client;
            Session = session;
            ModeRoundTripsExactly = modeRoundTripsExactly;
            RejectsWindowsUnsafeNames = rejectsWindowsUnsafeNames;
        }

        /// <summary>
        /// Gets a value indicating whether the server is a Windows-hosted OpenNFS local file system that rejects names ending
        /// in a space or dot (which Windows would otherwise silently trim) with NFS3ERR_INVAL.
        /// </summary>
        internal bool RejectsWindowsUnsafeNames { get; }

        internal string Name { get; }

        internal OpenNfsClient Client { get; }

        internal OpenNfsMountSession Session { get; }

        internal bool ModeRoundTripsExactly { get; }
    }

    /// <summary>
    /// Server-agnostic mounted-session scenarios for the v0.1.1 client surface. Every scenario runs unchanged against the
    /// in-process OpenNFS server and the Docker nfs-ganesha and Linux knfsd peers.
    /// </summary>
    internal static class MountSessionScenarioSupport
    {
        internal static async Task RunCoreScenariosAsync(MountSessionScenarioTarget target, CancellationToken cancellationToken)
        {
            string basePath = "/scenario-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            await RunStepAsync(target, "create-directory-with-parents", () => CreateDirectoryWithParentsAsync(target, basePath, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(target, "write-all-bytes-create-truncate", () => WriteAllBytesCreateAndTruncateAsync(target, basePath, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(target, "write-all-bytes-negative", () => WriteAllBytesNegativeAsync(target, basePath, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(target, "large-round-trip-and-ranged-reads", () => LargeRoundTripAndRangedReadsAsync(target, basePath, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(target, "open-read-stream", () => OpenReadStreamAsync(target, basePath, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(target, "stream-read-sequence", () => StreamReadSequenceAsync(target, basePath, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(target, "write-from-stream", () => WriteFromStreamAsync(target, basePath, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(target, "rename", () => RenameAsync(target, basePath, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(target, "exists", () => ExistsAsync(target, basePath, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(target, "set-length-and-times", () => SetLengthAndTimesAsync(target, basePath, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(target, "setattr-guard-and-mode", () => SetAttributesGuardAndModeAsync(target, basePath, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(target, "cancellation", () => CancellationAsync(target, basePath)).ConfigureAwait(false);
            await RunStepAsync(target, "special-character-names", () => SpecialCharacterNamesAsync(target, basePath, cancellationToken)).ConfigureAwait(false);
        }

        internal static async Task RunLargeDirectoryScenarioAsync(MountSessionScenarioTarget target, int entryCount, CancellationToken cancellationToken)
        {
            await RunStepAsync(target, "list-with-attributes-" + entryCount, async () =>
            {
                string directory = "/listing-" + Guid.NewGuid().ToString("N").Substring(0, 12);
                await target.Session.Directories.CreateDirectoryAsync(directory, createParents: true, cancellationToken).ConfigureAwait(false);
                byte[] directoryHandle = await target.Session.ResolvePathHandleOrThrowAsync(directory, "test", cancellationToken).ConfigureAwait(false);

                string[] names = Enumerable.Range(0, entryCount)
                    .Select(static index => "entry-" + index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture) + ".dat")
                    .ToArray();

                await ForEachParallelAsync(names, 16, async name =>
                {
                    OpenNfsV3CreatePathResult createResult =
                        await target.Client.Directories.CreateFileV3Async(directoryHandle, name, failIfExists: false, cancellationToken).ConfigureAwait(false);
                    if (!createResult.IsSuccess)
                    {
                        throw new InvalidOperationException("CREATE '" + name + "' failed with " + createResult.Status + ".");
                    }
                }).ConfigureAwait(false);

                await target.Session.Directories.CreateDirectoryAsync(directory + "/sub-directory", cancellationToken).ConfigureAwait(false);

                IReadOnlyList<OpenNfsV3DirectoryPlusEntry> entries =
                    await target.Session.Directories.ListWithAttributesAsync(directory, cancellationToken).ConfigureAwait(false);
                IReadOnlyList<OpenNfsV3DirectoryEntry> plainEntries =
                    await target.Session.Directories.ListAsync(directory, cancellationToken).ConfigureAwait(false);

                Require(entries.Count == entryCount + 1, "Expected " + (entryCount + 1) + " READDIRPLUS entries but found " + entries.Count + ".");
                Require(plainEntries.Count == entryCount + 1, "Expected " + (entryCount + 1) + " READDIR entries but found " + plainEntries.Count + ".");
                Require(entries.All(static entry => entry.Attributes is not null), "Expected every READDIRPLUS entry to carry attributes.");
                Require(entries.All(static entry => entry.FileHandle.Length > 0), "Expected every READDIRPLUS entry to carry a filehandle.");
                Require(entries.All(static entry => entry.Name != "." && entry.Name != ".."), "Expected dot entries to be excluded.");
                Require(
                    entries.Select(static entry => entry.Name).Distinct(StringComparer.Ordinal).Count() == entries.Count,
                    "Expected READDIRPLUS paging to return each entry exactly once.");

                HashSet<string> expectedNames = new HashSet<string>(names, StringComparer.Ordinal) { "sub-directory" };
                Require(entries.All(entry => expectedNames.Contains(entry.Name)), "Expected READDIRPLUS to return only created entries.");
                OpenNfsV3DirectoryPlusEntry subDirectory = entries.Single(static entry => entry.Name == "sub-directory");
                Require(subDirectory.Attributes!.FileType == OpenNfsV3FileType.Directory, "Expected the sub-directory entry to be reported as a directory.");
                Require(
                    entries.Where(static entry => entry.Name != "sub-directory").All(static entry => entry.Attributes!.FileType == OpenNfsV3FileType.RegularFile),
                    "Expected file entries to be reported as regular files.");
            }).ConfigureAwait(false);
        }

        internal static async Task RunConcurrencyScenarioAsync(MountSessionScenarioTarget target, int parallelism, CancellationToken cancellationToken)
        {
            await RunStepAsync(target, "concurrency-" + parallelism, async () =>
            {
                string directory = "/concurrency-" + Guid.NewGuid().ToString("N").Substring(0, 12);
                await target.Session.Directories.CreateDirectoryAsync(directory, createParents: true, cancellationToken).ConfigureAwait(false);

                int[] indexes = Enumerable.Range(0, parallelism).ToArray();
                Task[] tasks = indexes.Select(index => Task.Run(async () =>
                {
                    string path = directory + "/file-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin";
                    byte[] payload = CreatePayload(10_000 + (index * 997), seed: index);
                    await target.Session.Files.WriteAllBytesAsync(path, payload, (OpenNfsWriteStability)(index % 3), cancellationToken).ConfigureAwait(false);

                    for (int iteration = 0; iteration < 3; iteration++)
                    {
                        byte[] readBack = await target.Session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                        Require(readBack.AsSpan().SequenceEqual(payload), "Concurrent read of '" + path + "' returned the wrong payload.");

                        byte[] range = await target.Session.Files.ReadAsync(path, 100, 50, cancellationToken).ConfigureAwait(false);
                        Require(range.AsSpan().SequenceEqual(payload.AsSpan(100, 50)), "Concurrent ranged read of '" + path + "' returned the wrong bytes.");

                        Require(await target.Session.Metadata.ExistsAsync(path, cancellationToken).ConfigureAwait(false), "Expected '" + path + "' to exist.");
                        OpenNfsV3Attributes attributes = await target.Session.Metadata.GetAttributesAsync(path, cancellationToken).ConfigureAwait(false);
                        Require(attributes.SizeBytes == (ulong)payload.Length, "Concurrent GETATTR of '" + path + "' reported the wrong size.");
                    }
                }, cancellationToken)).ToArray();

                Task<IReadOnlyList<OpenNfsV3DirectoryPlusEntry>> listing = Task.Run(
                    () => target.Session.Directories.ListWithAttributesAsync(directory, cancellationToken),
                    cancellationToken);

                await Task.WhenAll(tasks).ConfigureAwait(false);
                _ = await listing.ConfigureAwait(false);

                IReadOnlyList<OpenNfsV3DirectoryPlusEntry> finalListing =
                    await target.Session.Directories.ListWithAttributesAsync(directory, cancellationToken).ConfigureAwait(false);
                Require(finalListing.Count == parallelism, "Expected " + parallelism + " files after the concurrency stress but found " + finalListing.Count + ".");
            }).ConfigureAwait(false);
        }

        internal static byte[] CreatePayload(int length, int seed)
        {
            byte[] payload = new byte[length];
            new Random(seed).NextBytes(payload);
            return payload;
        }

        internal static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        internal static async Task<OpenNfsV3StatusException> ExpectStatusAsync(Func<Task> action, params OpenNfsV3Status[] expectedStatuses)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (OpenNfsV3StatusException exception)
            {
                if (expectedStatuses.Length > 0 && !expectedStatuses.Contains(exception.Status))
                {
                    throw new InvalidOperationException(
                        "Expected NFSv3 status " + string.Join(" or ", expectedStatuses) + " but observed " + exception.Status + ".",
                        exception);
                }

                return exception;
            }

            throw new InvalidOperationException("Expected an OpenNfsV3StatusException (" + string.Join(" or ", expectedStatuses) + ") but the operation succeeded.");
        }

        internal static async Task<TException> ExpectAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException exception)
            {
                return exception;
            }

            throw new InvalidOperationException("Expected " + typeof(TException).Name + " but the operation succeeded.");
        }

        internal static async Task ForEachParallelAsync<T>(IReadOnlyList<T> items, int degreeOfParallelism, Func<T, Task> action)
        {
            using SemaphoreSlim gate = new SemaphoreSlim(degreeOfParallelism, degreeOfParallelism);
            List<Task> tasks = new List<Task>(items.Count);
            foreach (T item in items)
            {
                await gate.WaitAsync().ConfigureAwait(false);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await action(item).ConfigureAwait(false);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private static async Task RunStepAsync(MountSessionScenarioTarget target, string stepName, Func<Task> step)
        {
            try
            {
                await step().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    "Mounted-session scenario '" + stepName + "' failed against " + target.Name + ": " + exception.Message,
                    exception);
            }
        }

        private static async Task CreateDirectoryWithParentsAsync(MountSessionScenarioTarget target, string basePath, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = target.Session;
            await session.Directories.CreateDirectoryAsync(basePath + "/a/b/c", createParents: true, cancellationToken).ConfigureAwait(false);
            Require(await session.Metadata.ExistsAsync(basePath + "/a", cancellationToken).ConfigureAwait(false), "Expected the first ancestor to exist.");
            Require(await session.Metadata.ExistsAsync(basePath + "/a/b/c", cancellationToken).ConfigureAwait(false), "Expected the leaf directory to exist.");

            await session.Directories.CreateDirectoryAsync(basePath + "/a/b/c", createParents: true, cancellationToken).ConfigureAwait(false);
            await session.Directories.CreateDirectoryAsync(basePath + "/a/b", createParents: true, cancellationToken).ConfigureAwait(false);

            await ExpectStatusAsync(
                () => session.Directories.CreateDirectoryAsync(basePath + "/a/b/c", createParents: false, cancellationToken),
                OpenNfsV3Status.AlreadyExists).ConfigureAwait(false);
            await ExpectStatusAsync(
                () => session.Directories.CreateDirectoryAsync(basePath + "/missing/leaf", createParents: false, cancellationToken),
                OpenNfsV3Status.NoEntry).ConfigureAwait(false);

            await session.Files.WriteAllBytesAsync(basePath + "/a/blocker.txt", Encoding.UTF8.GetBytes("x"), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            await ExpectStatusAsync(
                () => session.Directories.CreateDirectoryAsync(basePath + "/a/blocker.txt/child", createParents: true, cancellationToken),
                OpenNfsV3Status.NotDirectory).ConfigureAwait(false);
            await ExpectStatusAsync(
                () => session.Directories.CreateDirectoryAsync(basePath + "/a/blocker.txt", createParents: true, cancellationToken),
                OpenNfsV3Status.AlreadyExists).ConfigureAwait(false);
        }

        private static async Task WriteAllBytesCreateAndTruncateAsync(MountSessionScenarioTarget target, string basePath, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = target.Session;
            string path = basePath + "/a/truncate.bin";

            Require(!await session.Metadata.ExistsAsync(path, cancellationToken).ConfigureAwait(false), "Expected the target not to exist before the first write.");

            byte[] original = CreatePayload(100, seed: 1);
            await session.Files.WriteAllBytesAsync(path, original, OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);
            byte[] afterCreate = await session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            Require(afterCreate.AsSpan().SequenceEqual(original), "Expected WriteAllBytesAsync to create the missing file with the exact payload.");

            byte[] shorter = CreatePayload(10, seed: 2);
            await session.Files.WriteAllBytesAsync(path, shorter, OpenNfsWriteStability.DataSync, cancellationToken).ConfigureAwait(false);
            byte[] afterShrink = await session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            Require(
                afterShrink.AsSpan().SequenceEqual(shorter),
                "Expected overwriting a 100-byte file with 10 bytes to leave exactly 10 bytes, but read " + afterShrink.Length + ".");
            OpenNfsV3Attributes attributes = await session.Metadata.GetAttributesAsync(path, cancellationToken).ConfigureAwait(false);
            Require(attributes.SizeBytes == 10UL, "Expected the truncated size to be 10 bytes but GETATTR reported " + attributes.SizeBytes + ".");

            await session.Files.WriteAllBytesAsync(path, Array.Empty<byte>(), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            byte[] afterEmpty = await session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            Require(afterEmpty.Length == 0, "Expected writing an empty payload to truncate the file to zero bytes, but read " + afterEmpty.Length + ".");

            string emptyNewPath = basePath + "/a/empty-new.bin";
            await session.Files.WriteAllBytesAsync(emptyNewPath, Array.Empty<byte>(), OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);
            Require(await session.Metadata.ExistsAsync(emptyNewPath, cancellationToken).ConfigureAwait(false), "Expected an empty write to create the file.");
            Require((await session.Metadata.GetAttributesAsync(emptyNewPath, cancellationToken).ConfigureAwait(false)).SizeBytes == 0, "Expected the new empty file to have size zero.");
        }

        private static async Task WriteAllBytesNegativeAsync(MountSessionScenarioTarget target, string basePath, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = target.Session;
            await ExpectStatusAsync(
                () => session.Files.WriteAllBytesAsync(basePath + "/no-such-directory/file.bin", new byte[] { 1 }, OpenNfsWriteStability.FileSync, cancellationToken),
                OpenNfsV3Status.NoEntry).ConfigureAwait(false);
            await ExpectStatusAsync(
                () => session.Files.WriteAllBytesAsync(basePath + "/a/b", new byte[] { 1 }, OpenNfsWriteStability.FileSync, cancellationToken),
                OpenNfsV3Status.IsDirectory).ConfigureAwait(false);
            await ExpectAsync<ArgumentNullException>(
                () => session.Files.WriteAllBytesAsync(basePath + "/a/null.bin", null!, OpenNfsWriteStability.FileSync, cancellationToken)).ConfigureAwait(false);
            await ExpectStatusAsync(
                () => session.Files.ReadAllBytesAsync(basePath + "/a/missing.bin", cancellationToken),
                OpenNfsV3Status.NoEntry).ConfigureAwait(false);
        }

        private static async Task LargeRoundTripAndRangedReadsAsync(MountSessionScenarioTarget target, string basePath, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = target.Session;
            string path = basePath + "/a/large.bin";
            byte[] payload = CreatePayload((3 * 1024 * 1024) + 123, seed: 3);

            await session.Files.WriteAllBytesAsync(path, payload, OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);
            byte[] readBack = await session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            Require(readBack.AsSpan().SequenceEqual(payload), "Expected a multi-chunk WriteAllBytesAsync/ReadAllBytesAsync round trip to preserve the payload.");

            byte[] head = await session.Files.ReadAsync(path, 0, 10, cancellationToken).ConfigureAwait(false);
            Require(head.AsSpan().SequenceEqual(payload.AsSpan(0, 10)), "Expected ReadAsync(0, 10) to return the first ten bytes.");

            byte[] middle = await session.Files.ReadAsync(path, 1_000_000, 300_000, cancellationToken).ConfigureAwait(false);
            Require(middle.AsSpan().SequenceEqual(payload.AsSpan(1_000_000, 300_000)), "Expected a multi-chunk ranged read to return the exact range.");

            byte[] tail = await session.Files.ReadAsync(path, (ulong)payload.Length - 5, 10, cancellationToken).ConfigureAwait(false);
            Require(tail.AsSpan().SequenceEqual(payload.AsSpan(payload.Length - 5, 5)), "Expected a read that crosses EOF to return only the remaining five bytes.");

            byte[] atEnd = await session.Files.ReadAsync(path, (ulong)payload.Length, 10, cancellationToken).ConfigureAwait(false);
            Require(atEnd.Length == 0, "Expected a read at EOF to return an empty array.");

            byte[] beyondEnd = await session.Files.ReadAsync(path, (ulong)payload.Length + 4096, 1, cancellationToken).ConfigureAwait(false);
            Require(beyondEnd.Length == 0, "Expected a read beyond EOF to return an empty array.");

            byte[] zeroCount = await session.Files.ReadAsync(path, 0, 0, cancellationToken).ConfigureAwait(false);
            Require(zeroCount.Length == 0, "Expected a zero-count read to return an empty array.");

            await ExpectAsync<ArgumentOutOfRangeException>(() => session.Files.ReadAsync(path, 0, -1, cancellationToken)).ConfigureAwait(false);
            await ExpectStatusAsync(() => session.Files.ReadAsync(basePath + "/a/missing.bin", 0, 1, cancellationToken), OpenNfsV3Status.NoEntry).ConfigureAwait(false);
            await ExpectStatusAsync(() => session.Files.ReadAsync(basePath + "/nope/missing.bin", 0, 1, cancellationToken), OpenNfsV3Status.NoEntry).ConfigureAwait(false);
            await ExpectStatusAsync(() => session.Files.ReadAsync(basePath + "/a/b", 0, 1, cancellationToken), OpenNfsV3Status.IsDirectory).ConfigureAwait(false);
        }

        private static async Task OpenReadStreamAsync(MountSessionScenarioTarget target, string basePath, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = target.Session;
            string path = basePath + "/a/stream-source.bin";
            byte[] payload = CreatePayload(300_001, seed: 4);
            await session.Files.WriteAllBytesAsync(path, payload, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);

            Stream stream = await session.Files.OpenReadAsync(path, cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                Require(stream.CanRead && stream.CanSeek && !stream.CanWrite, "Expected a readable, seekable, non-writable stream.");
                Require(stream.Length == payload.Length, "Expected the stream length to equal the file size at open.");

                using MemoryStream copy = new MemoryStream();
                await stream.CopyToAsync(copy, 7_777, cancellationToken).ConfigureAwait(false);
                Require(copy.ToArray().AsSpan().SequenceEqual(payload), "Expected copying the stream to reproduce the file.");
                Require(stream.Position == payload.Length, "Expected the stream position to be at the end after the copy.");

                stream.Seek(12_345, SeekOrigin.Begin);
                byte[] buffer = new byte[100];
                int read = await ReadFullyAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
                Require(read == 100 && buffer.AsSpan().SequenceEqual(payload.AsSpan(12_345, 100)), "Expected Seek(Begin) followed by a read to return the addressed bytes.");

                stream.Seek(-50, SeekOrigin.Current);
                read = await ReadFullyAsync(stream, buffer.AsMemory(0, 10), cancellationToken).ConfigureAwait(false);
                Require(read == 10 && buffer.AsSpan(0, 10).SequenceEqual(payload.AsSpan(12_395, 10)), "Expected Seek(Current) to move relative to the current position.");

                stream.Seek(-3, SeekOrigin.End);
                read = await ReadFullyAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
                Require(read == 3 && buffer.AsSpan(0, 3).SequenceEqual(payload.AsSpan(payload.Length - 3, 3)), "Expected a read near the end to return only the remaining bytes.");

                stream.Position = payload.Length + 1000;
                Require(await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) == 0, "Expected a read after seeking beyond the end to return zero bytes.");

                bool negativeSeekRejected = false;
                try
                {
                    stream.Seek(-1, SeekOrigin.Begin);
                }
                catch (IOException)
                {
                    negativeSeekRejected = true;
                }

                Require(negativeSeekRejected, "Expected seeking before the beginning to throw IOException.");

                stream.Position = 1;
                Require(stream.ReadByte() == payload[1], "Expected the synchronous ReadByte path to return the addressed byte.");

                bool writeRejected = false;
                try
                {
                    stream.Write(new byte[1], 0, 1);
                }
                catch (NotSupportedException)
                {
                    writeRejected = true;
                }

                Require(writeRejected, "Expected Write to throw NotSupportedException.");
            }

            stream.Dispose();
            await stream.DisposeAsync().ConfigureAwait(false);
            await ExpectAsync<ObjectDisposedException>(async () => _ = await stream.ReadAsync(new byte[1], cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

            string emptyPath = basePath + "/a/stream-empty.bin";
            await session.Files.WriteAllBytesAsync(emptyPath, Array.Empty<byte>(), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            await using (Stream emptyStream = await session.Files.OpenReadAsync(emptyPath, cancellationToken).ConfigureAwait(false))
            {
                Require(emptyStream.Length == 0 && await emptyStream.ReadAsync(new byte[16], cancellationToken).ConfigureAwait(false) == 0, "Expected an empty file stream to have length zero and read nothing.");
            }

            await ExpectStatusAsync(() => session.Files.OpenReadAsync(basePath + "/a/missing.bin", cancellationToken), OpenNfsV3Status.NoEntry).ConfigureAwait(false);
            await ExpectStatusAsync(() => session.Files.OpenReadAsync(basePath + "/a/b", cancellationToken), OpenNfsV3Status.IsDirectory).ConfigureAwait(false);
        }

        internal static async Task StreamReadSequenceAsync(MountSessionScenarioTarget target, string basePath, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = target.Session;
            string path = basePath + "/stream-sequence.bin";
            byte[] payload = CreatePayload((3 * 1024 * 1024) + 3, seed: 21);
            using (MemoryStream source = new MemoryStream(payload))
            {
                await session.Files.WriteAsync(path, source, null, OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);
            }

            Stream stream = await session.Files.OpenReadAsync(path, cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                Require(stream.CanSeek && stream.Length == payload.Length, "Expected a seekable stream with the written length.");

                stream.Seek((2 * 1024 * 1024) + 5, SeekOrigin.Begin);
                byte[] middle = new byte[100000];
                int read = await ReadExactlyWithArrayAsync(stream, middle, cancellationToken).ConfigureAwait(false);
                Require(read == middle.Length && middle.AsSpan().SequenceEqual(payload.AsSpan((2 * 1024 * 1024) + 5, middle.Length)), "Expected 100000 bytes at 2 MiB + 5.");

                stream.Position = 0;
                byte[] head = new byte[1000];
                read = await ReadExactlyWithArrayAsync(stream, head, cancellationToken).ConfigureAwait(false);
                Require(read == head.Length && head.AsSpan().SequenceEqual(payload.AsSpan(0, head.Length)), "Expected the first 1000 bytes after rewinding.");

                stream.Seek(-10, SeekOrigin.End);
                byte[] tail = new byte[10];
                read = await ReadExactlyWithArrayAsync(stream, tail, cancellationToken).ConfigureAwait(false);
                Require(read == 10 && tail.AsSpan().SequenceEqual(payload.AsSpan(payload.Length - 10)), "Expected the last 10 bytes.");
                Require(await stream.ReadAsync(new byte[16], 0, 16, cancellationToken).ConfigureAwait(false) == 0, "Expected a read at EOF to return 0.");

                stream.Seek(stream.Length + 100, SeekOrigin.Begin);
                Require(await stream.ReadAsync(new byte[16], 0, 16, cancellationToken).ConfigureAwait(false) == 0, "Expected a read beyond EOF to return 0.");
            }

            byte[] direct = await session.Files.ReadAsync(path, (ulong)payload.Length, 16, cancellationToken).ConfigureAwait(false);
            Require(direct.Length == 0, "Expected ReadAsync at EOF to return an empty array.");
            direct = await session.Files.ReadAsync(path, (ulong)payload.Length + 4096, 16, cancellationToken).ConfigureAwait(false);
            Require(direct.Length == 0, "Expected ReadAsync beyond EOF to return an empty array.");
            direct = await session.Files.ReadAsync(path, 7, 0, cancellationToken).ConfigureAwait(false);
            Require(direct.Length == 0, "Expected a zero-count ReadAsync to return an empty array.");
            byte[] whole = await session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            Require(whole.AsSpan().SequenceEqual(payload), "Expected the session to keep working after the stream sequence.");
        }

        private static async Task<int> ReadExactlyWithArrayAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer, total, buffer.Length - total, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            return total;
        }

        private static async Task WriteFromStreamAsync(MountSessionScenarioTarget target, string basePath, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = target.Session;
            string path = basePath + "/a/from-stream.bin";
            byte[] payload = CreatePayload(1_500_000, seed: 5);

            foreach (OpenNfsWriteStability stability in new[] { OpenNfsWriteStability.Unstable, OpenNfsWriteStability.DataSync, OpenNfsWriteStability.FileSync })
            {
                await session.Files.WriteAsync(path, new NonSeekableReadStream(payload, 7_777), length: null, stability, cancellationToken).ConfigureAwait(false);
                byte[] readBack = await session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                Require(readBack.AsSpan().SequenceEqual(payload), "Expected a non-seekable stream write (" + stability + ") to reproduce the source exactly.");
            }

            using (MemoryStream longer = new MemoryStream(payload))
            {
                await session.Files.WriteAsync(path, longer, length: 1000, OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);
                Require(longer.Position == 1000, "Expected an exact-length write to consume exactly the requested bytes.");
            }

            byte[] exact = await session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            Require(exact.AsSpan().SequenceEqual(payload.AsSpan(0, 1000)), "Expected an exact-length write to overwrite and truncate to exactly 1000 bytes.");

            using (MemoryStream offset = new MemoryStream(payload))
            {
                offset.Position = 500;
                await session.Files.WriteAsync(path, offset, length: null, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            }

            byte[] fromOffset = await session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            Require(fromOffset.AsSpan().SequenceEqual(payload.AsSpan(500)), "Expected a stream write to start at the source's current position.");

            await ExpectAsync<EndOfStreamException>(
                () => session.Files.WriteAsync(path, new NonSeekableReadStream(payload.AsSpan(0, 10).ToArray(), 3), length: 20, OpenNfsWriteStability.FileSync, cancellationToken)).ConfigureAwait(false);

            await session.Files.WriteAsync(path, new MemoryStream(Array.Empty<byte>()), length: null, OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);
            Require((await session.Metadata.GetAttributesAsync(path, cancellationToken).ConfigureAwait(false)).SizeBytes == 0, "Expected an empty source stream to produce an empty file.");

            await session.Files.WriteAsync(path, new MemoryStream(payload), length: 0, OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);
            Require((await session.Metadata.GetAttributesAsync(path, cancellationToken).ConfigureAwait(false)).SizeBytes == 0, "Expected a zero-length stream write to produce an empty file.");

            string createdPath = basePath + "/a/created-from-stream.bin";
            await session.Files.WriteAsync(createdPath, new MemoryStream(payload, 0, 4096), length: 4096, OpenNfsWriteStability.DataSync, cancellationToken).ConfigureAwait(false);
            Require((await session.Files.ReadAllBytesAsync(createdPath, cancellationToken).ConfigureAwait(false)).AsSpan().SequenceEqual(payload.AsSpan(0, 4096)), "Expected WriteAsync to create a missing file.");

            await ExpectAsync<ArgumentOutOfRangeException>(
                () => session.Files.WriteAsync(path, new MemoryStream(payload), length: -1, OpenNfsWriteStability.FileSync, cancellationToken)).ConfigureAwait(false);
            await ExpectAsync<ArgumentNullException>(
                () => session.Files.WriteAsync(path, null!, length: null, OpenNfsWriteStability.FileSync, cancellationToken)).ConfigureAwait(false);
            await ExpectStatusAsync(
                () => session.Files.WriteAsync(basePath + "/nope/x.bin", new MemoryStream(payload), length: null, OpenNfsWriteStability.FileSync, cancellationToken),
                OpenNfsV3Status.NoEntry).ConfigureAwait(false);
            await ExpectStatusAsync(
                () => session.Files.WriteAsync(basePath + "/a/b", new MemoryStream(payload), length: null, OpenNfsWriteStability.FileSync, cancellationToken),
                OpenNfsV3Status.IsDirectory).ConfigureAwait(false);
        }

        private static async Task RenameAsync(MountSessionScenarioTarget target, string basePath, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = target.Session;
            string source = basePath + "/a/rename-source.txt";
            string destination = basePath + "/a/b/rename-destination.txt";
            await session.Files.WriteAllBytesAsync(source, Encoding.UTF8.GetBytes("rename-me"), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);

            await session.Files.RenameAsync(source, destination, cancellationToken).ConfigureAwait(false);
            Require(!await session.Metadata.ExistsAsync(source, cancellationToken).ConfigureAwait(false), "Expected the rename source to disappear.");
            Require(
                Encoding.UTF8.GetString(await session.Files.ReadAllBytesAsync(destination, cancellationToken).ConfigureAwait(false)) == "rename-me",
                "Expected the renamed file to keep its contents.");

            string replacement = basePath + "/a/replacement.txt";
            await session.Files.WriteAllBytesAsync(replacement, Encoding.UTF8.GetBytes("replacement"), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            await session.Files.RenameAsync(replacement, destination, cancellationToken).ConfigureAwait(false);
            Require(
                Encoding.UTF8.GetString(await session.Files.ReadAllBytesAsync(destination, cancellationToken).ConfigureAwait(false)) == "replacement",
                "Expected renaming onto an existing file to replace it.");

            await session.Directories.CreateDirectoryAsync(basePath + "/dir-to-move/inner", createParents: true, cancellationToken).ConfigureAwait(false);
            await session.Files.RenameAsync(basePath + "/dir-to-move", basePath + "/a/moved-dir", cancellationToken).ConfigureAwait(false);
            Require(await session.Metadata.ExistsAsync(basePath + "/a/moved-dir/inner", cancellationToken).ConfigureAwait(false), "Expected directory renames to move the subtree.");
            Require(!await session.Metadata.ExistsAsync(basePath + "/dir-to-move", cancellationToken).ConfigureAwait(false), "Expected the old directory name to disappear.");

            await ExpectStatusAsync(
                () => session.Files.RenameAsync(basePath + "/a/does-not-exist.txt", basePath + "/a/whatever.txt", cancellationToken),
                OpenNfsV3Status.NoEntry).ConfigureAwait(false);
            await ExpectStatusAsync(
                () => session.Files.RenameAsync(destination, basePath + "/no-such-parent/x.txt", cancellationToken),
                OpenNfsV3Status.NoEntry).ConfigureAwait(false);
        }

        private static async Task ExistsAsync(MountSessionScenarioTarget target, string basePath, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = target.Session;
            Require(await session.Metadata.ExistsAsync("/", cancellationToken).ConfigureAwait(false), "Expected the export root to exist.");
            Require(await session.Metadata.ExistsAsync(basePath + "/a/blocker.txt", cancellationToken).ConfigureAwait(false), "Expected an existing file to exist.");
            Require(await session.Metadata.ExistsAsync(basePath + "/a/b", cancellationToken).ConfigureAwait(false), "Expected an existing directory to exist.");
            Require(!await session.Metadata.ExistsAsync(basePath + "/a/nothing-here", cancellationToken).ConfigureAwait(false), "Expected a missing entry not to exist.");
            Require(!await session.Metadata.ExistsAsync(basePath + "/missing-parent/child/leaf", cancellationToken).ConfigureAwait(false), "Expected a missing intermediate directory to report false.");
            Require(!await session.Metadata.ExistsAsync(basePath + "/a/blocker.txt/child", cancellationToken).ConfigureAwait(false), "Expected a path through a regular file (NOTDIR) to report false.");
            await ExpectAsync<ArgumentException>(() => session.Metadata.ExistsAsync(basePath + "/../escape", cancellationToken)).ConfigureAwait(false);
        }

        private static async Task SetLengthAndTimesAsync(MountSessionScenarioTarget target, string basePath, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = target.Session;
            string path = basePath + "/a/length.bin";
            byte[] payload = CreatePayload(1000, seed: 6);
            await session.Files.WriteAllBytesAsync(path, payload, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);

            await session.Metadata.SetLengthAsync(path, 100, cancellationToken).ConfigureAwait(false);
            byte[] shrunk = await session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            Require(shrunk.AsSpan().SequenceEqual(payload.AsSpan(0, 100)), "Expected SetLengthAsync to truncate to 100 bytes.");

            await session.Metadata.SetLengthAsync(path, 200, cancellationToken).ConfigureAwait(false);
            byte[] extended = await session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            Require(
                extended.Length == 200 && extended.AsSpan(0, 100).SequenceEqual(payload.AsSpan(0, 100)) && extended.AsSpan(100).IndexOfAnyExcept((byte)0) < 0,
                "Expected SetLengthAsync to extend the file with zero bytes.");

            await session.Metadata.SetLengthAsync(path, 0, cancellationToken).ConfigureAwait(false);
            Require((await session.Metadata.GetAttributesAsync(path, cancellationToken).ConfigureAwait(false)).SizeBytes == 0, "Expected SetLengthAsync(0) to empty the file.");

            DateTime modifyTime = new DateTime(2001, 2, 3, 4, 5, 6, 700, DateTimeKind.Utc);
            DateTime accessTime = new DateTime(2002, 3, 4, 5, 6, 7, 800, DateTimeKind.Utc);
            await session.Metadata.SetTimesAsync(path, accessTime, modifyTime, cancellationToken).ConfigureAwait(false);
            OpenNfsV3Attributes attributes = await session.Metadata.GetAttributesAsync(path, cancellationToken).ConfigureAwait(false);
            RequireClose(attributes.ModifyTime.ToDateTimeUtc(), modifyTime, "modification time");
            RequireClose(attributes.AccessTime.ToDateTimeUtc(), accessTime, "access time");

            DateTime secondModifyTime = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            await session.Metadata.SetTimesAsync(path, null, secondModifyTime, cancellationToken).ConfigureAwait(false);
            attributes = await session.Metadata.GetAttributesAsync(path, cancellationToken).ConfigureAwait(false);
            RequireClose(attributes.ModifyTime.ToDateTimeUtc(), secondModifyTime, "modification time after a modify-only update");
            RequireClose(attributes.AccessTime.ToDateTimeUtc(), accessTime, "access time left unchanged by a modify-only update");

            await session.Metadata.SetTimesAsync(basePath + "/a/b", accessTime, modifyTime, cancellationToken).ConfigureAwait(false);
            OpenNfsV3Attributes directoryAttributes = await session.Metadata.GetAttributesAsync(basePath + "/a/b", cancellationToken).ConfigureAwait(false);
            RequireClose(directoryAttributes.ModifyTime.ToDateTimeUtc(), modifyTime, "directory modification time");

            await session.Metadata.SetTimesAsync(path, null, null, cancellationToken).ConfigureAwait(false);
            await ExpectStatusAsync(() => session.Metadata.SetLengthAsync(basePath + "/a/missing.bin", 1, cancellationToken), OpenNfsV3Status.NoEntry).ConfigureAwait(false);
            await ExpectStatusAsync(() => session.Metadata.SetTimesAsync(basePath + "/a/missing.bin", accessTime, null, cancellationToken), OpenNfsV3Status.NoEntry).ConfigureAwait(false);
            await ExpectStatusAsync(
                () => session.Metadata.SetLengthAsync(basePath + "/a/b", 1, cancellationToken),
                OpenNfsV3Status.IsDirectory,
                OpenNfsV3Status.InvalidArgument,
                OpenNfsV3Status.BadType).ConfigureAwait(false);
            await ExpectAsync<ArgumentOutOfRangeException>(
                () => session.Metadata.SetTimesAsync(path, new DateTime(1960, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, cancellationToken)).ConfigureAwait(false);
        }

        private static async Task SetAttributesGuardAndModeAsync(MountSessionScenarioTarget target, string basePath, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = target.Session;
            string path = basePath + "/a/guarded.bin";
            await session.Files.WriteAllBytesAsync(path, CreatePayload(64, seed: 7), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            byte[] handle = await session.ResolvePathHandleOrThrowAsync(path, "test", cancellationToken).ConfigureAwait(false);

            OpenNfsV3GetAttributesResult before = await target.Client.Files.GetAttributesV3Async(handle, cancellationToken).ConfigureAwait(false);
            Require(before.IsSuccess && before.Attributes is not null, "Expected GETATTR to succeed before the guarded SETATTR.");
            OpenNfsV3Time staleChangeTime = new OpenNfsV3Time(before.Attributes!.ChangeTime.Seconds - 1000, 0);

            OpenNfsV3SetAttributesResult mismatched = await target.Client.Files.SetAttributesV3Async(
                handle,
                new OpenNfsV3SetAttributes(sizeBytes: 3),
                staleChangeTime,
                cancellationToken).ConfigureAwait(false);
            Require(mismatched.Status == OpenNfsV3Status.NotSynchronized, "Expected a ctime guard mismatch to return NFS3ERR_NOT_SYNC but observed " + mismatched.Status + ".");
            Require((await session.Metadata.GetAttributesAsync(path, cancellationToken).ConfigureAwait(false)).SizeBytes == 64, "Expected a rejected guarded SETATTR to leave the size unchanged.");

            OpenNfsV3SetAttributesResult matched = await target.Client.Files.SetAttributesV3Async(
                handle,
                new OpenNfsV3SetAttributes(sizeBytes: 3),
                before.Attributes.ChangeTime,
                cancellationToken).ConfigureAwait(false);
            Require(matched.IsSuccess, "Expected a matching ctime guard to allow the SETATTR but observed " + matched.Status + ".");
            Require(matched.Wcc?.After is not null && matched.Wcc.After.SizeBytes == 3, "Expected SETATTR weak-cache-consistency data to report the new size.");

            OpenNfsClientResult<OpenNfsV3SetAttributesResult> tryResult = await target.Client.Files.TrySetAttributesV3Async(
                handle,
                new OpenNfsV3SetAttributes(modifyTimeMode: OpenNfsV3TimeSetMode.SetToServerTime),
                guardChangeTime: null,
                cancellationToken).ConfigureAwait(false);
            Require(tryResult.IsSuccess && tryResult.Value is not null && tryResult.Value.IsSuccess, "Expected TrySetAttributesV3Async to return a successful envelope for a server-time update.");

            if (target.ModeRoundTripsExactly)
            {
                OpenNfsV3SetAttributesResult modeResult = await target.Client.Files.SetAttributesV3Async(
                    handle,
                    new OpenNfsV3SetAttributes(mode: Convert.ToUInt32("640", 8)),
                    guardChangeTime: null,
                    cancellationToken).ConfigureAwait(false);
                Require(modeResult.IsSuccess, "Expected a mode SETATTR to succeed but observed " + modeResult.Status + ".");
                uint mode = (await session.Metadata.GetAttributesAsync(path, cancellationToken).ConfigureAwait(false)).Mode & 0xFFFU;
                Require(mode == Convert.ToUInt32("640", 8), "Expected the mode to round-trip as 0640 but observed " + Convert.ToString(mode, 8) + ".");
            }
            else
            {
                OpenNfsV3SetAttributesResult readOnlyResult = await target.Client.Files.SetAttributesV3Async(
                    handle,
                    new OpenNfsV3SetAttributes(mode: Convert.ToUInt32("444", 8)),
                    guardChangeTime: null,
                    cancellationToken).ConfigureAwait(false);
                Require(readOnlyResult.IsSuccess, "Expected a read-only mode SETATTR to succeed but observed " + readOnlyResult.Status + ".");
                uint readOnlyMode = (await session.Metadata.GetAttributesAsync(path, cancellationToken).ConfigureAwait(false)).Mode;
                Require((readOnlyMode & Convert.ToUInt32("222", 8)) == 0, "Expected clearing the write bits to be reflected in GETATTR.");

                OpenNfsV3SetAttributesResult writableResult = await target.Client.Files.SetAttributesV3Async(
                    handle,
                    new OpenNfsV3SetAttributes(mode: Convert.ToUInt32("644", 8)),
                    guardChangeTime: null,
                    cancellationToken).ConfigureAwait(false);
                Require(writableResult.IsSuccess, "Expected a writable mode SETATTR to succeed but observed " + writableResult.Status + ".");
                uint writableMode = (await session.Metadata.GetAttributesAsync(path, cancellationToken).ConfigureAwait(false)).Mode;
                Require((writableMode & Convert.ToUInt32("200", 8)) != 0, "Expected restoring the owner write bit to be reflected in GETATTR.");
            }

            await session.Directories.DeleteFileAsync(path, cancellationToken).ConfigureAwait(false);
        }

        private static async Task CancellationAsync(MountSessionScenarioTarget target, string basePath)
        {
            OpenNfsMountSession session = target.Session;
            using CancellationTokenSource cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            CancellationToken token = cancelled.Token;
            string file = basePath + "/a/blocker.txt";

            await ExpectAsync<OperationCanceledException>(() => session.Files.ReadAsync(file, 0, 1, token)).ConfigureAwait(false);
            await ExpectAsync<OperationCanceledException>(() => session.Files.OpenReadAsync(file, token)).ConfigureAwait(false);
            await ExpectAsync<OperationCanceledException>(() => session.Files.WriteAsync(file, new MemoryStream(new byte[1]), null, OpenNfsWriteStability.FileSync, token)).ConfigureAwait(false);
            await ExpectAsync<OperationCanceledException>(() => session.Files.WriteAllBytesAsync(file, new byte[1], OpenNfsWriteStability.FileSync, token)).ConfigureAwait(false);
            await ExpectAsync<OperationCanceledException>(() => session.Files.RenameAsync(file, file + ".renamed", token)).ConfigureAwait(false);
            await ExpectAsync<OperationCanceledException>(() => session.Directories.ListWithAttributesAsync(basePath, token)).ConfigureAwait(false);
            await ExpectAsync<OperationCanceledException>(() => session.Directories.CreateDirectoryAsync(basePath + "/never", createParents: true, token)).ConfigureAwait(false);
            await ExpectAsync<OperationCanceledException>(() => session.Metadata.ExistsAsync(file, token)).ConfigureAwait(false);
            await ExpectAsync<OperationCanceledException>(() => session.Metadata.SetLengthAsync(file, 0, token)).ConfigureAwait(false);
            await ExpectAsync<OperationCanceledException>(() => session.Metadata.SetTimesAsync(file, DateTime.UtcNow, null, token)).ConfigureAwait(false);

            Require(await session.Metadata.ExistsAsync(file, CancellationToken.None).ConfigureAwait(false), "Expected cancelled operations not to mutate the file.");
            Require(!await session.Metadata.ExistsAsync(basePath + "/never", CancellationToken.None).ConfigureAwait(false), "Expected a cancelled directory create not to create anything.");
        }

        internal static readonly string[] PortableSpecialNames =
        {
            "  leading-spaces.txt",
            "internal   multiple   spaces.txt",
            "\u00FCn\u00EFc\u00F8d\u00E9-\u65E5\u672C\u8A9E-\U0001F600.txt",
            " x-leading-space-dir-sibling",
        };

        internal static readonly string[] WindowsUnsafeSpecialNames =
        {
            "trailing-space.txt ",
            "trailing-dot.",
            "both . ",
        };

        private static async Task SpecialCharacterNamesAsync(MountSessionScenarioTarget target, string basePath, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = target.Session;
            string directory = basePath + "/names/ x";
            await session.Directories.CreateDirectoryAsync(directory, createParents: true, cancellationToken).ConfigureAwait(false);
            Require(await session.Metadata.ExistsAsync(directory, cancellationToken).ConfigureAwait(false), "Expected a directory named ' x' to exist.");
            Require(!await session.Metadata.ExistsAsync(basePath + "/names/x", cancellationToken).ConfigureAwait(false), "Expected the directory ' x' not to be created as 'x'.");

            List<string> expectedNames = new List<string>();
            foreach (string name in PortableSpecialNames)
            {
                string path = directory + "/" + name;
                byte[] payload = Encoding.UTF8.GetBytes("payload:[" + name + "]");
                await session.Files.WriteAllBytesAsync(path, payload, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
                Require((await session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)).AsSpan().SequenceEqual(payload), "Expected '" + name + "' to round-trip its payload.");
                Require(await session.Metadata.ExistsAsync(path, cancellationToken).ConfigureAwait(false), "Expected '" + name + "' to exist.");
                string trimmed = name.Trim();
                if (trimmed.Length > 0 && !string.Equals(trimmed, name, StringComparison.Ordinal))
                {
                    Require(!await session.Metadata.ExistsAsync(directory + "/" + trimmed, cancellationToken).ConfigureAwait(false), "Expected '" + name + "' not to be stored under the trimmed name '" + trimmed + "'.");
                }

                expectedNames.Add(name);
            }

            foreach (string name in WindowsUnsafeSpecialNames)
            {
                string path = directory + "/" + name;
                byte[] payload = Encoding.UTF8.GetBytes("payload:[" + name + "]");
                string aliased = name.TrimEnd(' ', '.');
                if (target.RejectsWindowsUnsafeNames)
                {
                    await ExpectStatusAsync(
                        () => session.Files.WriteAllBytesAsync(path, payload, OpenNfsWriteStability.FileSync, cancellationToken),
                        OpenNfsV3Status.InvalidArgument).ConfigureAwait(false);
                    Require(!await session.Metadata.ExistsAsync(path, cancellationToken).ConfigureAwait(false), "Expected the rejected name '" + name + "' not to exist.");
                    await ExpectStatusAsync(
                        () => session.Directories.CreateDirectoryAsync(path, cancellationToken),
                        OpenNfsV3Status.InvalidArgument).ConfigureAwait(false);
                    await ExpectStatusAsync(
                        () => session.Files.RenameAsync(directory + "/" + PortableSpecialNames[0], path, cancellationToken),
                        OpenNfsV3Status.InvalidArgument).ConfigureAwait(false);
                }
                else
                {
                    await session.Files.WriteAllBytesAsync(path, payload, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
                    Require((await session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)).AsSpan().SequenceEqual(payload), "Expected '" + name + "' to round-trip its payload.");
                    Require(await session.Metadata.ExistsAsync(path, cancellationToken).ConfigureAwait(false), "Expected '" + name + "' to exist.");
                    expectedNames.Add(name);
                }

                Require(!await session.Metadata.ExistsAsync(directory + "/" + aliased, cancellationToken).ConfigureAwait(false), "Expected '" + name + "' never to alias '" + aliased + "'.");
            }

            string[] expected = expectedNames.OrderBy(static name => name, StringComparer.Ordinal).ToArray();
            IReadOnlyList<OpenNfsV3DirectoryPlusEntry> listing = await session.Directories.ListWithAttributesAsync(directory, cancellationToken).ConfigureAwait(false);
            string[] listed = listing.Select(static entry => entry.Name).OrderBy(static name => name, StringComparer.Ordinal).ToArray();
            Require(
                listed.SequenceEqual(expected, StringComparer.Ordinal),
                "Expected the listing to contain exactly [" + string.Join("|", expected) + "] but found [" + string.Join("|", listed) + "].");
            IReadOnlyList<OpenNfsV3DirectoryEntry> plainListing = await session.Directories.ListAsync(directory, cancellationToken).ConfigureAwait(false);
            Require(
                plainListing.Select(static entry => entry.Name).OrderBy(static name => name, StringComparer.Ordinal).SequenceEqual(expected, StringComparer.Ordinal),
                "Expected READDIR to list the special names verbatim.");

            string renameTarget = target.RejectsWindowsUnsafeNames ? "  renamed  target" : "  renamed  target ";
            await session.Files.RenameAsync(directory + "/" + PortableSpecialNames[1], directory + "/" + renameTarget, cancellationToken).ConfigureAwait(false);
            Require(await session.Metadata.ExistsAsync(directory + "/" + renameTarget, cancellationToken).ConfigureAwait(false), "Expected a rename to keep the space-padded target name verbatim.");
            Require(!await session.Metadata.ExistsAsync(directory + "/renamed  target", cancellationToken).ConfigureAwait(false), "Expected the renamed entry not to be trimmed.");
        }

        private static void RequireClose(DateTime actual, DateTime expected, string what)
        {
            if (Math.Abs((actual - expected).TotalMilliseconds) > 1.0)
            {
                throw new InvalidOperationException(
                    "Expected the " + what + " to be " + expected.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
                    + " but observed " + actual.ToString("O", System.Globalization.CultureInfo.InvariantCulture) + ".");
            }
        }

        private static async Task<int> ReadFullyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer.Slice(total), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            return total;
        }
    }

    /// <summary>
    /// Read-only, non-seekable stream that returns at most a fixed number of bytes per read, used to exercise
    /// stream-to-NFS writes that must fill chunks across several source reads.
    /// </summary>
    internal sealed class NonSeekableReadStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _maximumReadSize;
        private int _position;

        internal NonSeekableReadStream(byte[] data, int maximumReadSize)
        {
            _data = data;
            _maximumReadSize = maximumReadSize;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int toCopy = Math.Min(Math.Min(count, _maximumReadSize), _data.Length - _position);
            Array.Copy(_data, _position, buffer, offset, toCopy);
            _position += toCopy;
            return toCopy;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
