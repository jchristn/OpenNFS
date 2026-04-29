namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;

    internal static class Nfs40DirectoryListingSupport
    {
        internal static entry4? BuildEntryList(IReadOnlyList<entry4> entries)
        {
            if (entries.Count < 1)
            {
                return null;
            }

            entry4? head = null;
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                entry4 source = entries[index];
                head = new entry4
                {
                    cookie = source.cookie,
                    name = source.name,
                    attrs = source.attrs,
                    nextentry = head,
                };
            }

            return head;
        }

        internal static byte[] CreateCookieVerifier(
            string directorySourcePath,
            IReadOnlyList<NfsDirectoryEntryInfo> entries)
        {
            ArgumentNullException.ThrowIfNull(directorySourcePath);
            ArgumentNullException.ThrowIfNull(entries);

            StringBuilder builder = new StringBuilder(directorySourcePath);
            for (int index = 0; index < entries.Count; index++)
            {
                NfsDirectoryEntryInfo entry = entries[index];
                builder.Append('|');
                builder.Append(entry.Name);
                builder.Append(':');
                builder.Append(entry.PathInfo.Kind);
                builder.Append(':');
                builder.Append(entry.PathInfo.Length);
                builder.Append(':');
                builder.Append(entry.PathInfo.ChangeTimeUtc?.UtcTicks ?? 0L);
            }

            byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
            byte[] verifier = new byte[8];
            Array.Copy(digest, verifier, verifier.Length);
            return verifier;
        }

        internal static bool IsCookieValid(
            ulong requestedCookie,
            byte[] suppliedVerifier,
            byte[] currentVerifier,
            int entryCount)
        {
            ArgumentNullException.ThrowIfNull(suppliedVerifier);
            ArgumentNullException.ThrowIfNull(currentVerifier);

            if (requestedCookie == 0UL)
            {
                return true;
            }

            if (requestedCookie > (ulong)entryCount)
            {
                return false;
            }

            return suppliedVerifier.AsSpan().SequenceEqual(currentVerifier);
        }

        internal static int MeasureReadDirectoryLength(READDIR4res result)
        {
            ArgumentNullException.ThrowIfNull(result);

            XdrWriter writer = new XdrWriter();
            result.WriteTo(writer);
            return writer.ToArray().Length;
        }

        internal static ulong ReadCookie(nfs_cookie4? cookie)
        {
            return cookie?.Value ?? 0UL;
        }

        internal static byte[] ReadCookieVerifier(verifier4? verifier)
        {
            if (verifier?.Value is null || verifier.Value.Length < 1)
            {
                return new byte[8];
            }

            if (verifier.Value.Length == 8)
            {
                return verifier.Value.AsSpan().ToArray();
            }

            throw new ArgumentException("The NFSv4 cookie verifier must contain exactly eight bytes.", nameof(verifier));
        }
    }
}
