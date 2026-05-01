namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Immutable AUTH_SYS identity values used by the current public client surface.
    /// </summary>
    public sealed class OpenNfsAuthSysCredentials
    {
        private readonly uint[] _supplementaryGroupIds;

        /// <summary>
        /// Gets a reusable default AUTH_SYS identity based on the current machine name and zero-valued user and group IDs.
        /// </summary>
        public static OpenNfsAuthSysCredentials Default { get; } = new OpenNfsAuthSysCredentials(Environment.MachineName, 0, 0);

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsAuthSysCredentials"/> class.
        /// </summary>
        /// <param name="machineName">AUTH_SYS machine name.</param>
        /// <param name="userId">AUTH_SYS user ID.</param>
        /// <param name="groupId">AUTH_SYS primary group ID.</param>
        /// <param name="supplementaryGroupIds">Optional AUTH_SYS supplementary group IDs.</param>
        public OpenNfsAuthSysCredentials(
            string machineName,
            uint userId,
            uint groupId,
            IReadOnlyList<uint>? supplementaryGroupIds = null)
        {
            if (string.IsNullOrWhiteSpace(machineName))
            {
                throw new ArgumentException("The AUTH_SYS machine name must contain a non-empty value.", nameof(machineName));
            }

            MachineName = machineName;
            UserId = userId;
            GroupId = groupId;
            _supplementaryGroupIds = CopySupplementaryGroups(supplementaryGroupIds);
        }

        /// <summary>
        /// Gets the AUTH_SYS machine name.
        /// </summary>
        public string MachineName { get; }

        /// <summary>
        /// Gets the AUTH_SYS user ID.
        /// </summary>
        public uint UserId { get; }

        /// <summary>
        /// Gets the AUTH_SYS primary group ID.
        /// </summary>
        public uint GroupId { get; }

        /// <summary>
        /// Gets the AUTH_SYS supplementary group IDs.
        /// </summary>
        public IReadOnlyList<uint> SupplementaryGroupIds
        {
            get
            {
                return _supplementaryGroupIds;
            }
        }

        private static uint[] CopySupplementaryGroups(IReadOnlyList<uint>? supplementaryGroupIds)
        {
            if (supplementaryGroupIds is null || supplementaryGroupIds.Count == 0)
            {
                return Array.Empty<uint>();
            }

            uint[] copy = new uint[supplementaryGroupIds.Count];
            for (int index = 0; index < supplementaryGroupIds.Count; index++)
            {
                copy[index] = supplementaryGroupIds[index];
            }

            return copy;
        }
    }
}
