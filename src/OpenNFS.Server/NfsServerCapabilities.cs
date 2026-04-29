namespace OpenNFS.Server
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Server.Abstractions.Capabilities;

    /// <summary>
    /// Immutable catalog of optional host capabilities exposed by a configured server.
    /// </summary>
    public sealed class NfsServerCapabilities
    {
        private readonly NfsCapabilityKind[] _AdvertisedCapabilities;

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsServerCapabilities"/> class.
        /// </summary>
        /// <param name="locking">Optional locking capability contract.</param>
        /// <param name="acls">Optional ACL capability contract.</param>
        /// <param name="delegations">Optional delegations capability contract.</param>
        /// <param name="copyClone">Optional copy and clone capability contract.</param>
        /// <param name="sparse">Optional sparse-file capability contract.</param>
        /// <param name="idMapper">Optional identity-mapping capability contract.</param>
        public NfsServerCapabilities(
            INfsLocking? locking = null,
            INfsAcls? acls = null,
            INfsDelegations? delegations = null,
            INfsCopyClone? copyClone = null,
            INfsSparse? sparse = null,
            INfsIdMapper? idMapper = null)
        {
            Locking = locking;
            Acls = acls;
            Delegations = delegations;
            CopyClone = copyClone;
            Sparse = sparse;
            IdMapper = idMapper;
            _AdvertisedCapabilities = BuildAdvertisedCapabilities();
        }

        /// <summary>
        /// Gets the configured locking capability contract, if one is available.
        /// </summary>
        public INfsLocking? Locking { get; }

        /// <summary>
        /// Gets the configured ACL capability contract, if one is available.
        /// </summary>
        public INfsAcls? Acls { get; }

        /// <summary>
        /// Gets the configured delegations capability contract, if one is available.
        /// </summary>
        public INfsDelegations? Delegations { get; }

        /// <summary>
        /// Gets the configured copy and clone capability contract, if one is available.
        /// </summary>
        public INfsCopyClone? CopyClone { get; }

        /// <summary>
        /// Gets the configured sparse-file capability contract, if one is available.
        /// </summary>
        public INfsSparse? Sparse { get; }

        /// <summary>
        /// Gets the configured identity-mapping capability contract, if one is available.
        /// </summary>
        public INfsIdMapper? IdMapper { get; }

        /// <summary>
        /// Gets the ordered list of advertised optional capabilities.
        /// </summary>
        public IReadOnlyList<NfsCapabilityKind> AdvertisedCapabilities
        {
            get
            {
                return _AdvertisedCapabilities;
            }
        }

        /// <summary>
        /// Determines whether a given optional capability is available.
        /// </summary>
        /// <param name="capabilityKind">Capability to evaluate.</param>
        /// <returns>True when the capability is available; otherwise false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capabilityKind"/> is outside the defined enum range.</exception>
        public bool Supports(NfsCapabilityKind capabilityKind)
        {
            return capabilityKind switch
            {
                NfsCapabilityKind.Locking => Locking is not null,
                NfsCapabilityKind.Acls => Acls is not null,
                NfsCapabilityKind.Delegations => Delegations is not null,
                NfsCapabilityKind.CopyClone => CopyClone is not null,
                NfsCapabilityKind.SparseFiles => Sparse is not null,
                NfsCapabilityKind.IdMapping => IdMapper is not null,
                _ => throw new ArgumentOutOfRangeException(nameof(capabilityKind), capabilityKind, "The requested capability kind is not defined."),
            };
        }

        private NfsCapabilityKind[] BuildAdvertisedCapabilities()
        {
            List<NfsCapabilityKind> advertisedCapabilities = new List<NfsCapabilityKind>();

            if (Locking is not null)
            {
                advertisedCapabilities.Add(NfsCapabilityKind.Locking);
            }

            if (Acls is not null)
            {
                advertisedCapabilities.Add(NfsCapabilityKind.Acls);
            }

            if (Delegations is not null)
            {
                advertisedCapabilities.Add(NfsCapabilityKind.Delegations);
            }

            if (CopyClone is not null)
            {
                advertisedCapabilities.Add(NfsCapabilityKind.CopyClone);
            }

            if (Sparse is not null)
            {
                advertisedCapabilities.Add(NfsCapabilityKind.SparseFiles);
            }

            if (IdMapper is not null)
            {
                advertisedCapabilities.Add(NfsCapabilityKind.IdMapping);
            }

            return advertisedCapabilities.ToArray();
        }
    }
}
