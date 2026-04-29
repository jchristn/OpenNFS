namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Server;

    internal static class Nfs3ProcedureHandlerFactory
    {
        internal static IReadOnlyList<INfs3ProcedureHandler> CreateDefault(OpenNfsServer server)
        {
            ArgumentNullException.ThrowIfNull(server);
            Nfs3WriteStateTracker writeState = Nfs3WriteStateTracker.ForServer(server);

            return new INfs3ProcedureHandler[]
            {
                new Nfs3GetAttrProcedureHandler(server),
                new Nfs3SetAttrProcedureHandler(server),
                new Nfs3LookupProcedureHandler(server),
                new Nfs3AccessProcedureHandler(server),
                new Nfs3ReadProcedureHandler(server),
                new Nfs3ReadLinkProcedureHandler(server),
                new Nfs3WriteProcedureHandler(server, writeState),
                new Nfs3CreateProcedureHandler(server),
                new Nfs3MkdirProcedureHandler(server),
                new Nfs3SymLinkProcedureHandler(server),
                new Nfs3MknodProcedureHandler(server),
                new Nfs3RemoveProcedureHandler(server),
                new Nfs3RmDirProcedureHandler(server),
                new Nfs3LinkProcedureHandler(server),
                new Nfs3RenameProcedureHandler(server),
                new Nfs3ReadDirProcedureHandler(server),
                new Nfs3ReadDirPlusProcedureHandler(server),
                new Nfs3FsStatProcedureHandler(server),
                new Nfs3FsInfoProcedureHandler(server),
                new Nfs3PathConfProcedureHandler(server),
                new Nfs3CommitProcedureHandler(server, writeState),
            };
        }
    }
}
