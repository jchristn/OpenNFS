namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;

    internal static class OpenNfsMountV3ReplyDecoder
    {
        internal static IReadOnlyList<OpenNfsExportV3Entry> ReadExportList(ReadOnlyMemory<byte> encodedReply)
        {
            exports exportList = DecodePayload(encodedReply, "MOUNT v3 EXPORT", exports.ReadFrom);
            List<OpenNfsExportV3Entry> entries = new List<OpenNfsExportV3Entry>();

            exportnode? current = exportList.Value;
            while (current is not null)
            {
                entries.Add(new OpenNfsExportV3Entry(
                    ReadRequiredText(current.ex_dir?.Value, "exports.ex_dir"),
                    ReadAuthorizedClientGroups(current.ex_groups)));
                current = current.ex_next?.Value;
            }

            return entries.ToArray();
        }

        internal static IReadOnlyList<OpenNfsMountedExportV3Entry> ReadMountedExportList(ReadOnlyMemory<byte> encodedReply)
        {
            mountlist mountedExports = DecodePayload(encodedReply, "MOUNT v3 DUMP", mountlist.ReadFrom);
            List<OpenNfsMountedExportV3Entry> entries = new List<OpenNfsMountedExportV3Entry>();

            mountbody? current = mountedExports.Value;
            while (current is not null)
            {
                entries.Add(new OpenNfsMountedExportV3Entry(
                    ReadRequiredText(current.ml_hostname?.Value, "mountlist.ml_hostname"),
                    ReadRequiredText(current.ml_directory?.Value, "mountlist.ml_directory")));
                current = current.ml_next?.Value;
            }

            return entries.ToArray();
        }

        internal static OpenNfsMountV3Result ReadMountResult(ReadOnlyMemory<byte> encodedReply)
        {
            mountres3 result = DecodePayload(encodedReply, "MOUNT v3 MNT", mountres3.ReadFrom);
            mountstat3? statusValue = result.fhs_status;
            if (!statusValue.HasValue)
            {
                throw new InvalidDataException("The MOUNT v3 MNT result did not specify a mount status.");
            }

            OpenNfsMountV3Status status = MapStatus(statusValue.Value);
            if (status != OpenNfsMountV3Status.Ok)
            {
                return new OpenNfsMountV3Result(status);
            }

            mountres3_ok mountInfo = result.mountinfo
                ?? throw new InvalidDataException("The successful MOUNT v3 MNT result did not include mount info.");
            byte[] rootFileHandle = mountInfo.fhandle?.Value
                ?? throw new InvalidDataException("The successful MOUNT v3 MNT result did not include a root filehandle.");
            int[] authenticationFlavorNumbers = mountInfo.auth_flavors
                ?? throw new InvalidDataException("The successful MOUNT v3 MNT result did not include authentication flavors.");

            return new OpenNfsMountV3Result(
                status,
                rootFileHandle,
                ReadAuthenticationFlavors(authenticationFlavorNumbers));
        }

        internal static void ValidateUnmountAllReply(ReadOnlyMemory<byte> encodedReply)
        {
            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(encodedReply, "MOUNT v3 UMNTALL");
        }

        internal static void ValidateUnmountReply(ReadOnlyMemory<byte> encodedReply)
        {
            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(encodedReply, "MOUNT v3 UMNT");
        }

        private static T DecodePayload<T>(
            ReadOnlyMemory<byte> encodedReply,
            string operationName,
            Func<XdrReader, T> readPayload)
        {
            ReadOnlyMemory<byte> procedurePayload = OpenNfsRpcReplyDecoder.ReadAcceptedSuccessProcedurePayload(
                encodedReply,
                operationName);
            XdrReader reader = new XdrReader(procedurePayload);
            T decodedPayload = readPayload(reader);
            reader.EnsureFullyConsumed();
            return decodedPayload;
        }

        private static IReadOnlyList<string> ReadAuthorizedClientGroups(groups? groupList)
        {
            List<string> groups = new List<string>();
            groupnode? current = groupList?.Value;

            while (current is not null)
            {
                groups.Add(ReadRequiredText(current.gr_name?.Value, "exports.ex_groups.gr_name"));
                current = current.gr_next?.Value;
            }

            return groups.ToArray();
        }

        private static IReadOnlyList<OpenNfsRpcAuthenticationFlavor> ReadAuthenticationFlavors(int[] authenticationFlavorNumbers)
        {
            OpenNfsRpcAuthenticationFlavor[] authenticationFlavors = new OpenNfsRpcAuthenticationFlavor[authenticationFlavorNumbers.Length];
            for (int index = 0; index < authenticationFlavorNumbers.Length; index++)
            {
                authenticationFlavors[index] = (OpenNfsRpcAuthenticationFlavor)authenticationFlavorNumbers[index];
            }

            return authenticationFlavors;
        }

        private static string ReadRequiredText(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(
                    "The decoded " + fieldName + " field did not contain a non-empty text value.");
            }

            return value;
        }

        private static OpenNfsMountV3Status MapStatus(mountstat3 status)
        {
            switch (status)
            {
                case mountstat3.MNT3_OK:
                    return OpenNfsMountV3Status.Ok;
                case mountstat3.MNT3ERR_PERM:
                    return OpenNfsMountV3Status.PermissionDenied;
                case mountstat3.MNT3ERR_NOENT:
                    return OpenNfsMountV3Status.NoEntry;
                case mountstat3.MNT3ERR_IO:
                    return OpenNfsMountV3Status.Io;
                case mountstat3.MNT3ERR_ACCES:
                    return OpenNfsMountV3Status.AccessDenied;
                case mountstat3.MNT3ERR_NOTDIR:
                    return OpenNfsMountV3Status.NotDirectory;
                case mountstat3.MNT3ERR_INVAL:
                    return OpenNfsMountV3Status.InvalidArgument;
                case mountstat3.MNT3ERR_NAMETOOLONG:
                    return OpenNfsMountV3Status.NameTooLong;
                case mountstat3.MNT3ERR_NOTSUPP:
                    return OpenNfsMountV3Status.NotSupported;
                case mountstat3.MNT3ERR_SERVERFAULT:
                    return OpenNfsMountV3Status.ServerFault;
                default:
                    throw new InvalidDataException(
                        "The MOUNT v3 MNT result reported unsupported status '" + status.ToString() + "'.");
            }
        }
    }
}
