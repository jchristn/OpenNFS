namespace OpenNFS.Server.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Response context containing ACL support flags and the current ACL entries for a host-local path.
    /// </summary>
    public sealed class NfsGetAclResponse
    {
        private readonly NfsAclEntry[] _entries;

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsGetAclResponse"/> class.
        /// </summary>
        /// <param name="supportedAcls">Advertised ACL entry-type support flags.</param>
        /// <param name="entries">Current ACL entry set.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
        public NfsGetAclResponse(NfsAclSupport supportedAcls, IReadOnlyList<NfsAclEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
            SupportedAcls = supportedAcls;
            _entries = CopyEntries(entries, nameof(entries));
        }

        /// <summary>
        /// Gets the advertised ACL entry-type support flags.
        /// </summary>
        public NfsAclSupport SupportedAcls { get; }

        /// <summary>
        /// Gets the current ACL entry set.
        /// </summary>
        public IReadOnlyList<NfsAclEntry> Entries
        {
            get
            {
                return _entries;
            }
        }

        private static NfsAclEntry[] CopyEntries(IReadOnlyList<NfsAclEntry> entries, string parameterName)
        {
            NfsAclEntry[] copy = new NfsAclEntry[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                copy[index] = entries[index] ?? throw new ArgumentNullException(parameterName, "ACL entry collections cannot contain null entries.");
            }

            return copy;
        }
    }
}
