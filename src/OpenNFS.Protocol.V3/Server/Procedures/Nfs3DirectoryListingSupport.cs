namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Text;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;

    internal static class Nfs3DirectoryListingSupport
    {
        private const int CookieVerifierLength = 8;
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        internal static byte[] CreateCookieVerifier(string directorySourcePath, IReadOnlyList<NfsDirectoryEntryInfo> entries)
        {
            ArgumentNullException.ThrowIfNull(directorySourcePath);
            ArgumentNullException.ThrowIfNull(entries);

            ulong hash = ComputeHash(directorySourcePath);

            for (int index = 0; index < entries.Count; index++)
            {
                NfsDirectoryEntryInfo entry = entries[index];
                hash = UpdateHash(hash, entry.Name);
                hash = UpdateHash(hash, entry.PathInfo.Path);
                hash = UpdateHash(hash, entry.PathInfo.Kind.ToString());
                hash = UpdateHash(hash, entry.PathInfo.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            byte[] verifier = new byte[CookieVerifierLength];
            BinaryPrimitives.WriteUInt64BigEndian(verifier, hash);
            return verifier;
        }

        internal static bool IsCookieValid(
            ulong cookie,
            byte[] suppliedVerifier,
            byte[] currentVerifier,
            int entryCount)
        {
            ArgumentNullException.ThrowIfNull(suppliedVerifier);
            ArgumentNullException.ThrowIfNull(currentVerifier);

            if (cookie == 0)
            {
                return true;
            }

            if (cookie > (ulong)entryCount)
            {
                return false;
            }

            if (suppliedVerifier.Length != CookieVerifierLength || currentVerifier.Length != CookieVerifierLength)
            {
                return false;
            }

            return suppliedVerifier.AsSpan().SequenceEqual(currentVerifier);
        }

        internal static byte[] ReadCookieVerifier(cookieverf3? cookieVerifier)
        {
            if (cookieVerifier?.Value is null)
            {
                return new byte[CookieVerifierLength];
            }

            return cookieVerifier.Value.AsSpan().ToArray();
        }

        internal static ulong ReadCookie(cookie3? cookie)
        {
            return cookie?.Value?.Value ?? 0UL;
        }

        internal static int MeasureDirectoryInformationLength(dirlist3 directoryListing)
        {
            ArgumentNullException.ThrowIfNull(directoryListing);

            XdrWriter writer = new XdrWriter();
            directoryListing.WriteTo(writer);
            return writer.ToArray().Length;
        }

        internal static int MeasureReadDirectoryLength(READDIR3res result)
        {
            ArgumentNullException.ThrowIfNull(result);

            XdrWriter writer = new XdrWriter();
            result.WriteTo(writer);
            return writer.ToArray().Length;
        }

        internal static int MeasureReadDirectoryPlusLength(READDIRPLUS3res result)
        {
            ArgumentNullException.ThrowIfNull(result);

            XdrWriter writer = new XdrWriter();
            result.WriteTo(writer);
            return writer.ToArray().Length;
        }

        internal static entry3list BuildEntryList(IReadOnlyList<entry3> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            entry3list nextEntry = new entry3list();

            for (int index = entries.Count - 1; index >= 0; index--)
            {
                entry3 currentEntry = entries[index];
                currentEntry.nextentry = nextEntry;
                nextEntry = new entry3list
                {
                    Value = currentEntry,
                };
            }

            return nextEntry;
        }

        internal static entryplus3list BuildEntryPlusList(IReadOnlyList<entryplus3> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            entryplus3list nextEntry = new entryplus3list();

            for (int index = entries.Count - 1; index >= 0; index--)
            {
                entryplus3 currentEntry = entries[index];
                currentEntry.nextentry = nextEntry;
                nextEntry = new entryplus3list
                {
                    Value = currentEntry,
                };
            }

            return nextEntry;
        }

        private static ulong ComputeHash(string value)
        {
            return UpdateHash(FnvOffsetBasis, value);
        }

        private static ulong UpdateHash(ulong hash, string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            byte[] bytes = Encoding.UTF8.GetBytes(value);

            for (int index = 0; index < bytes.Length; index++)
            {
                hash ^= bytes[index];
                hash *= FnvPrime;
            }

            return hash;
        }
    }
}
