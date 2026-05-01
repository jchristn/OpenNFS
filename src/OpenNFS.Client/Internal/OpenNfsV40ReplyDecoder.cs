namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Xdr;

    internal static class OpenNfsV40ReplyDecoder
    {
        internal static OpenNfsV40AccessResult ReadAccessResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 ACCESS");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 ACCESS"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40AccessResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 ACCESS");
            ACCESS4res accessResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_ACCESS, "NFSv4.0 ACCESS").opaccess
                ?? throw new InvalidDataException("The successful NFSv4.0 ACCESS reply omitted the ACCESS result arm.");
            ACCESS4resok resok = accessResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 ACCESS reply omitted the ACCESS success arm.");
            return new OpenNfsV40AccessResult(
                overallStatus,
                (OpenNfsV40AccessMask)resok.supported,
                (OpenNfsV40AccessMask)resok.access);
        }

        internal static OpenNfsV40CreateResult ReadCreateResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 CREATE");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 CREATE"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40CreateResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 4, "NFSv4.0 CREATE");
            CREATE4res createResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_CREATE, "NFSv4.0 CREATE").opcreate
                ?? throw new InvalidDataException("The successful NFSv4.0 CREATE reply omitted the CREATE result arm.");
            CREATE4resok createResok = createResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 CREATE reply omitted the CREATE success arm.");
            GETFH4res getfhResult = ReadExpectedOperation(result, 2, nfs_opnum4.OP_GETFH, "NFSv4.0 CREATE").opgetfh
                ?? throw new InvalidDataException("The successful NFSv4.0 CREATE reply omitted the GETFH result arm.");
            GETFH4resok getfhResok = getfhResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 CREATE reply omitted the GETFH success arm.");
            GETATTR4res getattrResult = ReadExpectedOperation(result, 3, nfs_opnum4.OP_GETATTR, "NFSv4.0 CREATE").opgetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 CREATE reply omitted the GETATTR result arm.");
            GETATTR4resok getattrResok = getattrResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 CREATE reply omitted the GETATTR success arm.");

            return new OpenNfsV40CreateResult(
                overallStatus,
                MapChangeInfo(createResok.cinfo),
                ReadBitmapWords(createResok.attrset, "CREATE4resok.attrset"),
                ReadRequiredOpaque(getfhResok.@object?.Value, "GETFH4resok.object"),
                MapAttributes(getattrResok.obj_attributes));
        }

        internal static OpenNfsV40StateIdResult ReadCloseResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 CLOSE");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 CLOSE"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40StateIdResult(ReadTerminalStatus(result, overallStatus));
            }

            if (result.resarray is null || (result.resarray.Length != 1 && result.resarray.Length != 2))
            {
                throw new InvalidDataException(
                    "The successful NFSv4.0 CLOSE reply was expected to contain either one operation (CLOSE) or two operations (PUTFH + CLOSE).");
            }

            if (result.resarray.Length == 2)
            {
                ReadExpectedOperation(result, 0, nfs_opnum4.OP_PUTFH, "NFSv4.0 CLOSE");
            }

            int closeIndex = result.resarray.Length - 1;
            CLOSE4res closeResult = ReadExpectedOperation(result, closeIndex, nfs_opnum4.OP_CLOSE, "NFSv4.0 CLOSE").opclose
                ?? throw new InvalidDataException("The successful NFSv4.0 CLOSE reply omitted the CLOSE result arm.");
            return new OpenNfsV40StateIdResult(overallStatus, MapStateId(closeResult.open_stateid, "CLOSE4res.open_stateid"));
        }

        internal static OpenNfsV40DelegationReturnResult ReadDelegationReturnResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 DELEGRETURN");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 DELEGRETURN"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40DelegationReturnResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 DELEGRETURN");
            DELEGRETURN4res delegReturnResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_DELEGRETURN, "NFSv4.0 DELEGRETURN").opdelegreturn
                ?? throw new InvalidDataException("The successful NFSv4.0 DELEGRETURN reply omitted the DELEGRETURN result arm.");
            return new OpenNfsV40DelegationReturnResult(MapStatus(ReadRequiredStatus(delegReturnResult.status, "NFSv4.0 DELEGRETURN")));
        }

        internal static COMPOUND4res ReadCompoundResult(ReadOnlyMemory<byte> encodedReply, string operationName)
        {
            ReadOnlyMemory<byte> procedurePayload = OpenNfsRpcReplyDecoder.ReadAcceptedSuccessProcedurePayload(
                encodedReply,
                operationName);
            XdrReader reader = new XdrReader(procedurePayload);
            COMPOUND4res decodedPayload = COMPOUND4res.ReadFrom(reader);
            reader.EnsureFullyConsumed();
            return decodedPayload;
        }

        internal static OpenNfsV40StateIdResult ReadOpenConfirmResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 OPEN_CONFIRM");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 OPEN_CONFIRM"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40StateIdResult(ReadTerminalStatus(result, overallStatus));
            }

            if (result.resarray is null || (result.resarray.Length != 1 && result.resarray.Length != 2))
            {
                throw new InvalidDataException(
                    "The successful NFSv4.0 OPEN_CONFIRM reply was expected to contain either one operation (OPEN_CONFIRM) or two operations (PUTFH + OPEN_CONFIRM).");
            }

            if (result.resarray.Length == 2)
            {
                ReadExpectedOperation(result, 0, nfs_opnum4.OP_PUTFH, "NFSv4.0 OPEN_CONFIRM");
            }

            int openConfirmIndex = result.resarray.Length - 1;
            OPEN_CONFIRM4res openConfirmResult = ReadExpectedOperation(result, openConfirmIndex, nfs_opnum4.OP_OPEN_CONFIRM, "NFSv4.0 OPEN_CONFIRM").opopen_confirm
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN_CONFIRM reply omitted the OPEN_CONFIRM result arm.");
            OPEN_CONFIRM4resok resok = openConfirmResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN_CONFIRM reply omitted the OPEN_CONFIRM success arm.");
            return new OpenNfsV40StateIdResult(overallStatus, MapStateId(resok.open_stateid, "OPEN_CONFIRM4resok.open_stateid"));
        }

        internal static OpenNfsV40StateIdResult ReadOpenDowngradeResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 OPEN_DOWNGRADE");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 OPEN_DOWNGRADE"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40StateIdResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 1, "NFSv4.0 OPEN_DOWNGRADE");
            OPEN_DOWNGRADE4res openDowngradeResult = ReadExpectedOperation(result, 0, nfs_opnum4.OP_OPEN_DOWNGRADE, "NFSv4.0 OPEN_DOWNGRADE").opopen_downgrade
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN_DOWNGRADE reply omitted the OPEN_DOWNGRADE result arm.");
            OPEN_DOWNGRADE4resok resok = openDowngradeResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN_DOWNGRADE reply omitted the OPEN_DOWNGRADE success arm.");
            return new OpenNfsV40StateIdResult(overallStatus, MapStateId(resok.open_stateid, "OPEN_DOWNGRADE4resok.open_stateid"));
        }

        internal static OpenNfsV40OpenResult ReadOpenResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 OPEN");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 OPEN"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40OpenResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 4, "NFSv4.0 OPEN");
            OPEN4res openResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_OPEN, "NFSv4.0 OPEN").opopen
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN reply omitted the OPEN result arm.");
            OPEN4resok openResok = openResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN reply omitted the OPEN success arm.");
            GETFH4res getfhResult = ReadExpectedOperation(result, 2, nfs_opnum4.OP_GETFH, "NFSv4.0 OPEN").opgetfh
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN reply omitted the GETFH result arm.");
            GETFH4resok getfhResok = getfhResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN reply omitted the GETFH success arm.");
            GETATTR4res getattrResult = ReadExpectedOperation(result, 3, nfs_opnum4.OP_GETATTR, "NFSv4.0 OPEN").opgetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN reply omitted the GETATTR result arm.");
            GETATTR4resok getattrResok = getattrResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN reply omitted the GETATTR success arm.");

            return new OpenNfsV40OpenResult(
                overallStatus,
                MapStateId(openResok.stateid, "OPEN4resok.stateid"),
                requiresConfirmation: (openResok.rflags & (uint)Nfs40Constants.OPEN4_RESULT_CONFIRM) != 0U,
                directoryChangeInfo: MapChangeInfo(openResok.cinfo),
                appliedAttributeMaskWords: ReadBitmapWords(openResok.attrset, "OPEN4resok.attrset"),
                objectFileHandle: ReadRequiredOpaque(getfhResok.@object?.Value, "GETFH4resok.object"),
                objectAttributes: MapAttributes(getattrResok.obj_attributes),
                delegation: MapDelegation(openResok.delegation));
        }

        internal static OpenNfsV40GetAttributesResult ReadGetAttributesResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 GETATTR");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 GETATTR"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40GetAttributesResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 GETATTR");
            GETATTR4res getattrResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_GETATTR, "NFSv4.0 GETATTR").opgetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 GETATTR reply omitted the GETATTR result arm.");
            GETATTR4resok resok = getattrResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 GETATTR reply omitted the GETATTR success arm.");
            return new OpenNfsV40GetAttributesResult(overallStatus, MapAttributes(resok.obj_attributes));
        }

        internal static OpenNfsV40GetAclResult ReadGetAclResult(ReadOnlyMemory<byte> encodedReply)
        {
            OpenNfsV40GetAttributesResult attributesResult = ReadGetAttributesResult(encodedReply);
            if (!attributesResult.IsSuccess)
            {
                return new OpenNfsV40GetAclResult(attributesResult.Status);
            }

            OpenNfsV40Attributes attributes = attributesResult.Attributes
                ?? throw new InvalidDataException("The successful NFSv4.0 GETACL helper reply omitted the decoded attribute envelope.");

            if (!attributes.AclSupport.HasValue)
            {
                throw new InvalidDataException("The successful NFSv4.0 GETACL helper reply omitted the ACL support attribute.");
            }

            return new OpenNfsV40GetAclResult(
                attributesResult.Status,
                attributes.AclSupport.Value,
                attributes.AclEntries);
        }

        internal static OpenNfsV40LookupResult ReadGetRootResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 PUTROOTFH");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 PUTROOTFH"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40LookupResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 3, "NFSv4.0 PUTROOTFH");
            GETFH4res getfhResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_GETFH, "NFSv4.0 PUTROOTFH").opgetfh
                ?? throw new InvalidDataException("The successful NFSv4.0 PUTROOTFH reply omitted the GETFH result arm.");
            GETFH4resok getfhResok = getfhResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 PUTROOTFH reply omitted the GETFH success arm.");
            GETATTR4res getattrResult = ReadExpectedOperation(result, 2, nfs_opnum4.OP_GETATTR, "NFSv4.0 PUTROOTFH").opgetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 PUTROOTFH reply omitted the GETATTR result arm.");
            GETATTR4resok getattrResok = getattrResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 PUTROOTFH reply omitted the GETATTR success arm.");

            return new OpenNfsV40LookupResult(
                overallStatus,
                ReadRequiredOpaque(getfhResok.@object?.Value, "GETFH4resok.object"),
                MapAttributes(getattrResok.obj_attributes));
        }

        internal static OpenNfsV40LinkResult ReadLinkResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 LINK");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 LINK"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40LinkResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 4, "NFSv4.0 LINK");
            LINK4res linkResult = ReadExpectedOperation(result, 3, nfs_opnum4.OP_LINK, "NFSv4.0 LINK").oplink
                ?? throw new InvalidDataException("The successful NFSv4.0 LINK reply omitted the LINK result arm.");
            LINK4resok resok = linkResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 LINK reply omitted the LINK success arm.");
            return new OpenNfsV40LinkResult(overallStatus, MapChangeInfo(resok.cinfo));
        }

        internal static OpenNfsV40LookupResult ReadLookupResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 LOOKUP");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 LOOKUP"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40LookupResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 4, "NFSv4.0 LOOKUP");
            GETFH4res getfhResult = ReadExpectedOperation(result, 2, nfs_opnum4.OP_GETFH, "NFSv4.0 LOOKUP").opgetfh
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUP reply omitted the GETFH result arm.");
            GETFH4resok getfhResok = getfhResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUP reply omitted the GETFH success arm.");
            GETATTR4res getattrResult = ReadExpectedOperation(result, 3, nfs_opnum4.OP_GETATTR, "NFSv4.0 LOOKUP").opgetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUP reply omitted the GETATTR result arm.");
            GETATTR4resok getattrResok = getattrResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUP reply omitted the GETATTR success arm.");

            return new OpenNfsV40LookupResult(
                overallStatus,
                ReadRequiredOpaque(getfhResok.@object?.Value, "GETFH4resok.object"),
                MapAttributes(getattrResok.obj_attributes));
        }

        internal static OpenNfsV40LookupResult ReadLookupParentResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 LOOKUPP");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 LOOKUPP"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40LookupResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 4, "NFSv4.0 LOOKUPP");
            GETFH4res getfhResult = ReadExpectedOperation(result, 2, nfs_opnum4.OP_GETFH, "NFSv4.0 LOOKUPP").opgetfh
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUPP reply omitted the GETFH result arm.");
            GETFH4resok getfhResok = getfhResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUPP reply omitted the GETFH success arm.");
            GETATTR4res getattrResult = ReadExpectedOperation(result, 3, nfs_opnum4.OP_GETATTR, "NFSv4.0 LOOKUPP").opgetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUPP reply omitted the GETATTR result arm.");
            GETATTR4resok getattrResok = getattrResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUPP reply omitted the GETATTR success arm.");

            return new OpenNfsV40LookupResult(
                overallStatus,
                ReadRequiredOpaque(getfhResok.@object?.Value, "GETFH4resok.object"),
                MapAttributes(getattrResok.obj_attributes));
        }

        internal static OpenNfsV40SecurityInfoResult ReadSecurityInfoResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 SECINFO");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 SECINFO"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40SecurityInfoResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 SECINFO");
            SECINFO4res securityInfoResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_SECINFO, "NFSv4.0 SECINFO").opsecinfo
                ?? throw new InvalidDataException("The successful NFSv4.0 SECINFO reply omitted the SECINFO result arm.");
            SECINFO4resok resok = securityInfoResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 SECINFO reply omitted the SECINFO success arm.");

            return new OpenNfsV40SecurityInfoResult(overallStatus, MapSecurityFlavors(resok.Value));
        }

        internal static OpenNfsV40ReadDirectoryResult ReadReadDirectoryResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 READDIR");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 READDIR"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40ReadDirectoryResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 READDIR");
            READDIR4res readDirectoryResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_READDIR, "NFSv4.0 READDIR").opreaddir
                ?? throw new InvalidDataException("The successful NFSv4.0 READDIR reply omitted the READDIR result arm.");
            READDIR4resok resok = readDirectoryResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 READDIR reply omitted the READDIR success arm.");
            dirlist4 reply = resok.reply
                ?? throw new InvalidDataException("The successful NFSv4.0 READDIR reply omitted the directory listing payload.");

            return new OpenNfsV40ReadDirectoryResult(
                overallStatus,
                ReadRequiredFixedOpaque(resok.cookieverf?.Value, "READDIR4resok.cookieverf", 8),
                MapDirectoryEntries(reply.entries),
                reply.eof);
        }

        internal static OpenNfsV40ReadLinkResult ReadReadLinkResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 READLINK");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 READLINK"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40ReadLinkResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 READLINK");
            READLINK4res readLinkResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_READLINK, "NFSv4.0 READLINK").opreadlink
                ?? throw new InvalidDataException("The successful NFSv4.0 READLINK reply omitted the READLINK result arm.");
            READLINK4resok resok = readLinkResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 READLINK reply omitted the READLINK success arm.");
            return new OpenNfsV40ReadLinkResult(overallStatus, ReadRequiredUtf8(resok.link?.Value, "READLINK4resok.link"));
        }

        internal static OpenNfsV40LockResult ReadLockResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 LOCK");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 LOCK"));

            if (overallStatus == OpenNfsV40Status.Ok)
            {
                EnsureSuccessOperationCount(result, 2, "NFSv4.0 LOCK");
                LOCK4res lockResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_LOCK, "NFSv4.0 LOCK").oplock
                    ?? throw new InvalidDataException("The successful NFSv4.0 LOCK reply omitted the LOCK result arm.");
                LOCK4resok resok = lockResult.resok4
                    ?? throw new InvalidDataException("The successful NFSv4.0 LOCK reply omitted the LOCK success arm.");
                return new OpenNfsV40LockResult(overallStatus, MapStateId(resok.lock_stateid, "LOCK4resok.lock_stateid"));
            }

            if (overallStatus == OpenNfsV40Status.Denied)
            {
                EnsureSuccessOperationCount(result, 2, "NFSv4.0 LOCK");
                LOCK4res lockResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_LOCK, "NFSv4.0 LOCK").oplock
                    ?? throw new InvalidDataException("The denied NFSv4.0 LOCK reply omitted the LOCK result arm.");
                return new OpenNfsV40LockResult(
                    overallStatus,
                    conflict: MapLockConflict(lockResult.denied, "LOCK4res.denied"));
            }

            return new OpenNfsV40LockResult(ReadTerminalStatus(result, overallStatus));
        }

        internal static OpenNfsV40LockResult ReadLockTestResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 LOCKT");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 LOCKT"));

            if (overallStatus == OpenNfsV40Status.Ok)
            {
                EnsureSuccessOperationCount(result, 2, "NFSv4.0 LOCKT");
                return new OpenNfsV40LockResult(overallStatus);
            }

            if (overallStatus == OpenNfsV40Status.Denied)
            {
                EnsureSuccessOperationCount(result, 2, "NFSv4.0 LOCKT");
                LOCKT4res lockTestResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_LOCKT, "NFSv4.0 LOCKT").oplockt
                    ?? throw new InvalidDataException("The denied NFSv4.0 LOCKT reply omitted the LOCKT result arm.");
                return new OpenNfsV40LockResult(
                    overallStatus,
                    conflict: MapLockConflict(lockTestResult.denied, "LOCKT4res.denied"));
            }

            return new OpenNfsV40LockResult(ReadTerminalStatus(result, overallStatus));
        }

        internal static OpenNfsV40LockResult ReadLockUnlockResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 LOCKU");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 LOCKU"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40LockResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 LOCKU");
            LOCKU4res unlockResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_LOCKU, "NFSv4.0 LOCKU").oplocku
                ?? throw new InvalidDataException("The successful NFSv4.0 LOCKU reply omitted the LOCKU result arm.");
            return new OpenNfsV40LockResult(overallStatus, MapStateId(unlockResult.lock_stateid, "LOCKU4res.lock_stateid"));
        }

        internal static OpenNfsV40ReadResult ReadReadResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 READ");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 READ"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40ReadResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 READ");
            READ4res readResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_READ, "NFSv4.0 READ").opread
                ?? throw new InvalidDataException("The successful NFSv4.0 READ reply omitted the READ result arm.");
            READ4resok resok = readResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 READ reply omitted the READ success arm.");
            ReadOnlyMemory<byte> data = ReadRequiredOpaque(resok.data, "READ4resok.data", allowEmpty: true);
            return new OpenNfsV40ReadResult(overallStatus, (uint)data.Length, resok.eof, data);
        }

        internal static OpenNfsV40WriteResult ReadWriteResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 WRITE");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 WRITE"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40WriteResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 WRITE");
            WRITE4res writeResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_WRITE, "NFSv4.0 WRITE").opwrite
                ?? throw new InvalidDataException("The successful NFSv4.0 WRITE reply omitted the WRITE result arm.");
            WRITE4resok resok = writeResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 WRITE reply omitted the WRITE success arm.");
            return new OpenNfsV40WriteResult(
                overallStatus,
                ReadRequiredUInt32(resok.count?.Value, "WRITE4resok.count"),
                MapWriteStability(resok.committed),
                ReadRequiredFixedOpaque(resok.writeverf?.Value, "WRITE4resok.writeverf", 8));
        }

        internal static OpenNfsV40CommitResult ReadCommitResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 COMMIT");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 COMMIT"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40CommitResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 COMMIT");
            COMMIT4res commitResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_COMMIT, "NFSv4.0 COMMIT").opcommit
                ?? throw new InvalidDataException("The successful NFSv4.0 COMMIT reply omitted the COMMIT result arm.");
            COMMIT4resok resok = commitResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 COMMIT reply omitted the COMMIT success arm.");
            return new OpenNfsV40CommitResult(
                overallStatus,
                ReadRequiredFixedOpaque(resok.writeverf?.Value, "COMMIT4resok.writeverf", 8));
        }

        internal static OpenNfsV40SetAclResult ReadSetAclResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 SETATTR ACL");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 SETATTR ACL"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40SetAclResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 SETATTR ACL");
            SETATTR4res setAttributeResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_SETATTR, "NFSv4.0 SETATTR ACL").opsetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 SETATTR reply omitted the SETATTR result arm.");

            return new OpenNfsV40SetAclResult(
                overallStatus,
                ReadBitmapWords(setAttributeResult.attrsset, "SETATTR4res.attrsset"));
        }

        internal static OpenNfsV40SetIdentityResult ReadSetIdentityResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 SETATTR owner/owner_group");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 SETATTR owner/owner_group"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40SetIdentityResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 SETATTR owner/owner_group");
            SETATTR4res setAttributeResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_SETATTR, "NFSv4.0 SETATTR owner/owner_group").opsetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 SETATTR reply omitted the SETATTR result arm.");

            return new OpenNfsV40SetIdentityResult(
                overallStatus,
                ReadBitmapWords(setAttributeResult.attrsset, "SETATTR4res.attrsset"));
        }

        internal static OpenNfsV40DirectoryMutationResult ReadRemoveResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 REMOVE");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 REMOVE"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40DirectoryMutationResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 REMOVE");
            REMOVE4res removeResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_REMOVE, "NFSv4.0 REMOVE").opremove
                ?? throw new InvalidDataException("The successful NFSv4.0 REMOVE reply omitted the REMOVE result arm.");
            REMOVE4resok resok = removeResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 REMOVE reply omitted the REMOVE success arm.");
            return new OpenNfsV40DirectoryMutationResult(overallStatus, MapChangeInfo(resok.cinfo));
        }

        internal static OpenNfsV40RenameResult ReadRenameResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 RENAME");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 RENAME"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40RenameResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 4, "NFSv4.0 RENAME");
            RENAME4res renameResult = ReadExpectedOperation(result, 3, nfs_opnum4.OP_RENAME, "NFSv4.0 RENAME").oprename
                ?? throw new InvalidDataException("The successful NFSv4.0 RENAME reply omitted the RENAME result arm.");
            RENAME4resok resok = renameResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 RENAME reply omitted the RENAME success arm.");
            return new OpenNfsV40RenameResult(
                overallStatus,
                MapChangeInfo(resok.source_cinfo),
                MapChangeInfo(resok.target_cinfo));
        }

        internal static OpenNfsV40SessionResult ReadRenewResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 RENEW");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 RENEW"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40SessionResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 1, "NFSv4.0 RENEW");
            RENEW4res renewResult = ReadExpectedOperation(result, 0, nfs_opnum4.OP_RENEW, "NFSv4.0 RENEW").oprenew
                ?? throw new InvalidDataException("The successful NFSv4.0 RENEW reply omitted the RENEW result arm.");
            return new OpenNfsV40SessionResult(MapStatus(ReadRequiredStatus(renewResult.status, "NFSv4.0 RENEW")));
        }

        internal static OpenNfsV40SetClientIdResult ReadSetClientIdResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 SETCLIENTID");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 SETCLIENTID"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40SetClientIdResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 1, "NFSv4.0 SETCLIENTID");
            SETCLIENTID4res setClientIdResult = ReadExpectedOperation(result, 0, nfs_opnum4.OP_SETCLIENTID, "NFSv4.0 SETCLIENTID").opsetclientid
                ?? throw new InvalidDataException("The successful NFSv4.0 SETCLIENTID reply omitted the SETCLIENTID result arm.");
            SETCLIENTID4resok resok = setClientIdResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 SETCLIENTID reply omitted the SETCLIENTID success arm.");
            clientid4 clientId = resok.clientid
                ?? throw new InvalidDataException("The successful NFSv4.0 SETCLIENTID reply omitted the assigned clientid.");
            return new OpenNfsV40SetClientIdResult(
                overallStatus,
                clientId.Value,
                ReadRequiredFixedOpaque(resok.setclientid_confirm?.Value, "SETCLIENTID4resok.setclientid_confirm", 8));
        }

        internal static OpenNfsV40SessionResult ReadSetClientIdConfirmResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 SETCLIENTID_CONFIRM");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 SETCLIENTID_CONFIRM"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40SessionResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 1, "NFSv4.0 SETCLIENTID_CONFIRM");
            SETCLIENTID_CONFIRM4res confirmResult = ReadExpectedOperation(result, 0, nfs_opnum4.OP_SETCLIENTID_CONFIRM, "NFSv4.0 SETCLIENTID_CONFIRM").opsetclientid_confirm
                ?? throw new InvalidDataException("The successful NFSv4.0 SETCLIENTID_CONFIRM reply omitted the SETCLIENTID_CONFIRM result arm.");
            return new OpenNfsV40SessionResult(MapStatus(ReadRequiredStatus(confirmResult.status, "NFSv4.0 SETCLIENTID_CONFIRM")));
        }

        private static OpenNfsV40Attributes MapAttributes(fattr4? value)
        {
            if (value is null)
            {
                throw new InvalidDataException("The successful NFSv4.0 reply omitted a required fattr4 payload.");
            }

            List<int> attributeIds = ReadAttributeIds(value.attrmask);
            XdrReader reader = new XdrReader(value.attr_vals?.Value ?? Array.Empty<byte>());
            uint[] supportedAttributeMaskWords = Array.Empty<uint>();
            OpenNfsV40FileType? fileType = null;
            OpenNfsV40AclSupport? aclSupport = null;
            IReadOnlyList<OpenNfsV40AclEntry> aclEntries = Array.Empty<OpenNfsV40AclEntry>();
            ulong? changeId = null;
            ulong? sizeBytes = null;
            ReadOnlyMemory<byte> fileHandle = ReadOnlyMemory<byte>.Empty;
            string? owner = null;
            string? ownerGroup = null;

            for (int index = 0; index < attributeIds.Count; index++)
            {
                switch (attributeIds[index])
                {
                    case (int)Nfs40Constants.FATTR4_SUPPORTED_ATTRS:
                        supportedAttributeMaskWords = ReadBitmapWords(
                            fattr4_supported_attrs.ReadFrom(reader).Value,
                            "fattr4_supported_attrs");
                        break;
                    case (int)Nfs40Constants.FATTR4_TYPE:
                        fileType = (OpenNfsV40FileType)(int)ReadRequiredEnum(
                            fattr4_type.ReadFrom(reader).Value,
                            "fattr4_type.Value");
                        break;
                    case (int)Nfs40Constants.FATTR4_ACL:
                        aclEntries = MapAclEntries(fattr4_acl.ReadFrom(reader));
                        break;
                    case (int)Nfs40Constants.FATTR4_ACLSUPPORT:
                        aclSupport = (OpenNfsV40AclSupport)fattr4_aclsupport.ReadFrom(reader).Value;
                        break;
                    case (int)Nfs40Constants.FATTR4_CHANGE:
                        changeId = ReadRequiredUInt64(
                            fattr4_change.ReadFrom(reader).Value?.Value,
                            "fattr4_change.Value");
                        break;
                    case (int)Nfs40Constants.FATTR4_SIZE:
                        sizeBytes = fattr4_size.ReadFrom(reader).Value;
                        break;
                    case (int)Nfs40Constants.FATTR4_FILEHANDLE:
                        fileHandle = ReadRequiredOpaque(
                            fattr4_filehandle.ReadFrom(reader).Value?.Value,
                            "fattr4_filehandle.Value");
                        break;
                    case (int)Nfs40Constants.FATTR4_OWNER:
                        owner = ReadRequiredUtf8(
                            fattr4_owner.ReadFrom(reader).Value?.Value?.Value,
                            "fattr4_owner.Value");
                        break;
                    case (int)Nfs40Constants.FATTR4_OWNER_GROUP:
                        ownerGroup = ReadRequiredUtf8(
                            fattr4_owner_group.ReadFrom(reader).Value?.Value?.Value,
                            "fattr4_owner_group.Value");
                        break;
                    default:
                        throw new InvalidDataException(
                            "The decoded NFSv4.0 attribute payload reported unsupported attribute id "
                            + attributeIds[index] + ".");
                }
            }

            reader.EnsureFullyConsumed();
            return new OpenNfsV40Attributes(
                supportedAttributeMaskWords,
                fileType,
                aclSupport,
                aclEntries,
                changeId,
                sizeBytes,
                fileHandle,
                owner,
                ownerGroup);
        }

        private static IReadOnlyList<OpenNfsV40AclEntry> MapAclEntries(fattr4_acl value)
        {
            nfsace4[] entries = value.Value ?? Array.Empty<nfsace4>();
            OpenNfsV40AclEntry[] mappedEntries = new OpenNfsV40AclEntry[entries.Length];

            for (int index = 0; index < entries.Length; index++)
            {
                nfsace4 entry = entries[index] ?? throw new InvalidDataException("The decoded NFSv4.0 ACL payload contained a null ACE entry.");
                mappedEntries[index] = new OpenNfsV40AclEntry(
                    (OpenNfsV40AclEntryType)ReadRequiredUInt32(entry.type?.Value, "nfsace4.type"),
                    (OpenNfsV40AclEntryFlags)ReadRequiredUInt32(entry.flag?.Value, "nfsace4.flag"),
                    (OpenNfsV40AclPermissionMask)ReadRequiredUInt32(entry.access_mask?.Value, "nfsace4.access_mask"),
                    ReadRequiredUtf8(entry.who?.Value?.Value, "nfsace4.who"));
            }

            return mappedEntries;
        }

        private static OpenNfsV40ChangeInfo MapChangeInfo(change_info4? value)
        {
            if (value is null)
            {
                throw new InvalidDataException("The successful NFSv4.0 reply omitted a required change_info4 payload.");
            }

            return new OpenNfsV40ChangeInfo(
                value.atomic,
                ReadRequiredUInt64(value.before?.Value, "change_info4.before"),
                ReadRequiredUInt64(value.after?.Value, "change_info4.after"));
        }

        private static OpenNfsV40Delegation? MapDelegation(open_delegation4? value)
        {
            if (value?.delegation_type is null || value.delegation_type == open_delegation_type4.OPEN_DELEGATE_NONE)
            {
                return null;
            }

            return value.delegation_type switch
            {
                open_delegation_type4.OPEN_DELEGATE_READ => new OpenNfsV40Delegation(
                    OpenNfsV40DelegationType.Read,
                    MapStateId(value.read?.stateid, "open_delegation4.read.stateid"),
                    value.read?.recall ?? false),
                open_delegation_type4.OPEN_DELEGATE_WRITE => new OpenNfsV40Delegation(
                    OpenNfsV40DelegationType.Write,
                    MapStateId(value.write?.stateid, "open_delegation4.write.stateid"),
                    value.write?.recall ?? false),
                _ => null,
            };
        }

        private static OpenNfsV40LockConflict MapLockConflict(LOCK4denied? value, string fieldName)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            lock_owner4 owner = value.owner
                ?? throw new InvalidDataException("The decoded " + fieldName + ".owner field was required but missing.");
            clientid4 clientId = owner.clientid
                ?? throw new InvalidDataException("The decoded " + fieldName + ".owner.clientid field was required but missing.");
            return new OpenNfsV40LockConflict(
                clientId.Value,
                ReadRequiredOpaque(owner.owner, fieldName + ".owner.owner"),
                ReadRequiredUInt64(value.offset?.Value, fieldName + ".offset"),
                ReadRequiredUInt64(value.length?.Value, fieldName + ".length"),
                (OpenNfsV40LockType)(int)ReadRequiredEnum(value.locktype, fieldName + ".locktype"));
        }

        private static IReadOnlyList<OpenNfsV40SecurityFlavorInfo> MapSecurityFlavors(secinfo4[]? values)
        {
            if (values is null || values.Length == 0)
            {
                throw new InvalidDataException("The successful NFSv4.0 SECINFO reply omitted every advertised security flavor.");
            }

            OpenNfsV40SecurityFlavorInfo[] mappedFlavors = new OpenNfsV40SecurityFlavorInfo[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                mappedFlavors[index] = MapSecurityFlavor(values[index], "SECINFO4resok.Value[" + index + "]");
            }

            return mappedFlavors;
        }

        private static OpenNfsV40SecurityFlavorInfo MapSecurityFlavor(secinfo4 value, string fieldName)
        {
            OpenNfsRpcAuthenticationFlavor flavor = (OpenNfsRpcAuthenticationFlavor)value.flavor;
            if (flavor != OpenNfsRpcAuthenticationFlavor.RpcSecGss)
            {
                return new OpenNfsV40SecurityFlavorInfo(flavor);
            }

            rpcsec_gss_info flavorInfo = value.flavor_info
                ?? throw new InvalidDataException("The decoded " + fieldName + ".flavor_info field was required but missing.");

            return new OpenNfsV40SecurityFlavorInfo(
                flavor,
                (OpenNfsRpcGssService)(int)ReadRequiredEnum(flavorInfo.service, fieldName + ".flavor_info.service"),
                ReadRequiredUInt32(flavorInfo.qop?.Value, fieldName + ".flavor_info.qop"),
                ReadRequiredOpaque(flavorInfo.oid?.Value, fieldName + ".flavor_info.oid"));
        }

        private static OpenNfsV40StateId MapStateId(stateid4? value, string fieldName)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return new OpenNfsV40StateId(
                value.seqid,
                ReadRequiredFixedOpaque(value.other, fieldName + ".other", 12));
        }

        private static IReadOnlyList<OpenNfsV40DirectoryEntry> MapDirectoryEntries(entry4? entries)
        {
            List<OpenNfsV40DirectoryEntry> mappedEntries = new List<OpenNfsV40DirectoryEntry>();
            entry4? current = entries;

            while (current is not null)
            {
                mappedEntries.Add(
                    new OpenNfsV40DirectoryEntry(
                        current.cookie?.Value ?? 0UL,
                        ReadRequiredUtf8(current.name?.Value?.Value?.Value, "entry4.name"),
                        MapAttributes(current.attrs)));
                current = current.nextentry;
            }

            return mappedEntries.ToArray();
        }

        private static OpenNfsV40Status MapStatus(nfsstat4 value)
        {
            return (OpenNfsV40Status)(int)value;
        }

        private static OpenNfsWriteStability MapWriteStability(stable_how4? value)
        {
            stable_how4 stableValue = ReadRequiredEnum(value, "stable_how4");
            return stableValue switch
            {
                stable_how4.UNSTABLE4 => OpenNfsWriteStability.Unstable,
                stable_how4.DATA_SYNC4 => OpenNfsWriteStability.DataSync,
                stable_how4.FILE_SYNC4 => OpenNfsWriteStability.FileSync,
                _ => throw new InvalidDataException(
                    "The decoded NFSv4.0 stable_how4 value '" + stableValue.ToString() + "' is not supported."),
            };
        }

        private static nfs_resop4 ReadExpectedOperation(
            COMPOUND4res result,
            int index,
            nfs_opnum4 expectedOperation,
            string operationName)
        {
            nfs_resop4[] operations = result.resarray
                ?? throw new InvalidDataException("The " + operationName + " reply omitted the result array.");
            if (operations.Length <= index)
            {
                throw new InvalidDataException(
                    "The " + operationName + " reply omitted result index " + index + ".");
            }

            nfs_resop4 operation = operations[index];
            if (operation.resop != expectedOperation)
            {
                throw new InvalidDataException(
                    "The " + operationName + " reply reported operation "
                    + operation.resop?.ToString() + " at result index " + index
                    + " instead of " + expectedOperation.ToString() + ".");
            }

            return operation;
        }

        private static List<int> ReadAttributeIds(bitmap4? bitmap)
        {
            List<int> attributeIds = new List<int>();
            uint[] words = bitmap?.Value ?? Array.Empty<uint>();

            for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                uint word = words[wordIndex];
                for (int bitIndex = 0; bitIndex < 32; bitIndex++)
                {
                    if ((word & (1U << bitIndex)) != 0)
                    {
                        attributeIds.Add((wordIndex * 32) + bitIndex);
                    }
                }
            }

            return attributeIds;
        }

        private static uint[] ReadBitmapWords(bitmap4? bitmap, string fieldName)
        {
            if (bitmap?.Value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return bitmap.Value.AsSpan().ToArray();
        }

        private static T ReadRequiredEnum<T>(T? value, string fieldName)
            where T : struct
        {
            if (!value.HasValue)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        private static byte[] ReadRequiredFixedOpaque(byte[]? value, string fieldName, int expectedLength)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            if (value.Length != expectedLength)
            {
                throw new InvalidDataException(
                    "The decoded " + fieldName + " field must contain exactly " + expectedLength + " byte(s).");
            }

            return value.AsSpan().ToArray();
        }

        private static ReadOnlyMemory<byte> ReadRequiredOpaque(byte[]? value, string fieldName, bool allowEmpty = false)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            if (!allowEmpty && value.Length < 1)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field must not be empty.");
            }

            return new ReadOnlyMemory<byte>(value.AsSpan().ToArray());
        }

        private static nfsstat4 ReadRequiredStatus(nfsstat4? value, string operationName)
        {
            return ReadRequiredEnum(value, operationName + " status");
        }

        private static ulong ReadRequiredUInt64(ulong? value, string fieldName)
        {
            if (!value.HasValue)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        private static uint ReadRequiredUInt32(uint? value, string fieldName)
        {
            if (!value.HasValue)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return value.Value;
        }

        private static string ReadRequiredUtf8(byte[]? value, string fieldName)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return Encoding.UTF8.GetString(value);
        }

        private static OpenNfsV40Status ReadTerminalStatus(COMPOUND4res result, OpenNfsV40Status fallbackStatus)
        {
            if (result.resarray is null || result.resarray.Length < 1)
            {
                return fallbackStatus;
            }

            nfs_resop4 terminalOperation = result.resarray[result.resarray.Length - 1];
            nfsstat4? terminalStatus = terminalOperation.resop switch
            {
                nfs_opnum4.OP_ACCESS => terminalOperation.opaccess?.status,
                nfs_opnum4.OP_CLOSE => terminalOperation.opclose?.status,
                nfs_opnum4.OP_COMMIT => terminalOperation.opcommit?.status,
                nfs_opnum4.OP_CREATE => terminalOperation.opcreate?.status,
                nfs_opnum4.OP_DELEGPURGE => terminalOperation.opdelegpurge?.status,
                nfs_opnum4.OP_DELEGRETURN => terminalOperation.opdelegreturn?.status,
                nfs_opnum4.OP_GETATTR => terminalOperation.opgetattr?.status,
                nfs_opnum4.OP_GETFH => terminalOperation.opgetfh?.status,
                nfs_opnum4.OP_ILLEGAL => terminalOperation.opillegal?.status,
                nfs_opnum4.OP_LINK => terminalOperation.oplink?.status,
                nfs_opnum4.OP_LOCK => terminalOperation.oplock?.status,
                nfs_opnum4.OP_LOCKT => terminalOperation.oplockt?.status,
                nfs_opnum4.OP_LOCKU => terminalOperation.oplocku?.status,
                nfs_opnum4.OP_LOOKUP => terminalOperation.oplookup?.status,
                nfs_opnum4.OP_LOOKUPP => terminalOperation.oplookupp?.status,
                nfs_opnum4.OP_NVERIFY => terminalOperation.opnverify?.status,
                nfs_opnum4.OP_OPEN => terminalOperation.opopen?.status,
                nfs_opnum4.OP_OPENATTR => terminalOperation.opopenattr?.status,
                nfs_opnum4.OP_OPEN_CONFIRM => terminalOperation.opopen_confirm?.status,
                nfs_opnum4.OP_OPEN_DOWNGRADE => terminalOperation.opopen_downgrade?.status,
                nfs_opnum4.OP_PUTFH => terminalOperation.opputfh?.status,
                nfs_opnum4.OP_PUTPUBFH => terminalOperation.opputpubfh?.status,
                nfs_opnum4.OP_PUTROOTFH => terminalOperation.opputrootfh?.status,
                nfs_opnum4.OP_READ => terminalOperation.opread?.status,
                nfs_opnum4.OP_READDIR => terminalOperation.opreaddir?.status,
                nfs_opnum4.OP_READLINK => terminalOperation.opreadlink?.status,
                nfs_opnum4.OP_RELEASE_LOCKOWNER => terminalOperation.oprelease_lockowner?.status,
                nfs_opnum4.OP_REMOVE => terminalOperation.opremove?.status,
                nfs_opnum4.OP_RENAME => terminalOperation.oprename?.status,
                nfs_opnum4.OP_RENEW => terminalOperation.oprenew?.status,
                nfs_opnum4.OP_RESTOREFH => terminalOperation.oprestorefh?.status,
                nfs_opnum4.OP_SAVEFH => terminalOperation.opsavefh?.status,
                nfs_opnum4.OP_SECINFO => terminalOperation.opsecinfo?.status,
                nfs_opnum4.OP_SETATTR => terminalOperation.opsetattr?.status,
                nfs_opnum4.OP_SETCLIENTID => terminalOperation.opsetclientid?.status,
                nfs_opnum4.OP_SETCLIENTID_CONFIRM => terminalOperation.opsetclientid_confirm?.status,
                nfs_opnum4.OP_VERIFY => terminalOperation.opverify?.status,
                nfs_opnum4.OP_WRITE => terminalOperation.opwrite?.status,
                _ => null,
            };

            return terminalStatus.HasValue
                ? MapStatus(terminalStatus.Value)
                : fallbackStatus;
        }

        private static void EnsureSuccessOperationCount(COMPOUND4res result, int expectedCount, string operationName)
        {
            nfs_resop4[] operations = result.resarray
                ?? throw new InvalidDataException("The " + operationName + " reply omitted the result array.");

            if (operations.Length != expectedCount)
            {
                throw new InvalidDataException(
                    "The successful " + operationName + " reply reported "
                    + operations.Length + " operation result(s) instead of " + expectedCount + ".");
            }
        }
    }
}
