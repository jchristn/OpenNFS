namespace OpenNFS.Protocol.V3.Procedures
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Protocol.V3.Generated;

    internal static class Nfs3ProcedureCatalog
    {
        private static readonly Nfs3ProcedureDescriptor[] _Procedures = new[]
        {
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_NULL, "NFSPROC3_NULL"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_GETATTR, "NFSPROC3_GETATTR"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_SETATTR, "NFSPROC3_SETATTR"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LOOKUP, "NFSPROC3_LOOKUP"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_ACCESS, "NFSPROC3_ACCESS"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READLINK, "NFSPROC3_READLINK"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ, "NFSPROC3_READ"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_WRITE, "NFSPROC3_WRITE"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_CREATE, "NFSPROC3_CREATE"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_MKDIR, "NFSPROC3_MKDIR"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_SYMLINK, "NFSPROC3_SYMLINK"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_MKNOD, "NFSPROC3_MKNOD"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_REMOVE, "NFSPROC3_REMOVE"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_RMDIR, "NFSPROC3_RMDIR"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_RENAME, "NFSPROC3_RENAME"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LINK, "NFSPROC3_LINK"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READDIR, "NFSPROC3_READDIR"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READDIRPLUS, "NFSPROC3_READDIRPLUS"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_FSSTAT, "NFSPROC3_FSSTAT"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_FSINFO, "NFSPROC3_FSINFO"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_PATHCONF, "NFSPROC3_PATHCONF"),
            new Nfs3ProcedureDescriptor((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_COMMIT, "NFSPROC3_COMMIT"),
        };

        private static readonly Dictionary<uint, Nfs3ProcedureDescriptor> _ProceduresByNumber = BuildProcedureLookup(_Procedures);

        internal static IReadOnlyList<Nfs3ProcedureDescriptor> All
        {
            get
            {
                return _Procedures;
            }
        }

        internal static bool TryGetByProcedureNumber(uint procedureNumber, out Nfs3ProcedureDescriptor? descriptor)
        {
            return _ProceduresByNumber.TryGetValue(procedureNumber, out descriptor);
        }

        private static Dictionary<uint, Nfs3ProcedureDescriptor> BuildProcedureLookup(IReadOnlyCollection<Nfs3ProcedureDescriptor> procedures)
        {
            Dictionary<uint, Nfs3ProcedureDescriptor> lookup = new Dictionary<uint, Nfs3ProcedureDescriptor>(procedures.Count);

            foreach (Nfs3ProcedureDescriptor descriptor in procedures)
            {
                lookup.Add(descriptor.ProcedureNumber, descriptor);
            }

            return lookup;
        }
    }
}
