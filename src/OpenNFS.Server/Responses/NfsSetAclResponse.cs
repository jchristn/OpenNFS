namespace OpenNFS.Server.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Response context containing the effective ACL support flags and entry set after an ACL update.
    /// </summary>
    public sealed class NfsSetAclResponse
    {
        private readonly NfsAclEntry[] _entries;

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsSetAclResponse"/> class.
        /// </summary>
        /// <param name="supportedAcls">Advertised ACL entry-type support flags after the update.</param>
        /// <param name="entries">Effective ACL entry set after the update.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
        public NfsSetAclResponse(NfsAclSupport supportedAcls, IReadOnlyList<NfsAclEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
            SupportedAcls = supportedAcls;
            _entries = CopyEntries(entries, nameof(entries));
        }

        /// <summary>
        /// Gets the advertised ACL entry-type support flags after the update.
        /// </summary>
        public NfsAclSupport SupportedAcls { get; }

        /// <summary>
        /// Gets the effective ACL entry set after the update.
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
