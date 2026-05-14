namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V3.Generated;
    using static OpenNFS.Client.Internal.OpenNfsV3ReplyDecodingSupport;

    internal static class OpenNfsV3MetadataReplyDecoder
    {
        internal static OpenNfsV3GetAttributesResult ReadGetAttributesResult(ReadOnlyMemory<byte> encodedReply)
        {
            GETATTR3res result = DecodePayload(encodedReply, "NFSv3 GETATTR", GETATTR3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 GETATTR"));
            if (status != OpenNfsV3Status.Ok)
            {
                return new OpenNfsV3GetAttributesResult(status);
            }

            GETATTR3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 GETATTR result omitted the resok arm.");
            return new OpenNfsV3GetAttributesResult(
                status,
                MapRequiredAttributes(resok.obj_attributes, "GETATTR3res.resok.obj_attributes"));
        }

        internal static OpenNfsV3AccessResult ReadAccessResult(ReadOnlyMemory<byte> encodedReply)
        {
            ACCESS3res result = DecodePayload(encodedReply, "NFSv3 ACCESS", ACCESS3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 ACCESS"));

            if (status != OpenNfsV3Status.Ok)
            {
                ACCESS3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 ACCESS result omitted the resfail arm.");
                return new OpenNfsV3AccessResult(
                    status,
                    MapPostOperationAttributes(resfail.obj_attributes, "ACCESS3res.resfail.obj_attributes"));
            }

            ACCESS3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 ACCESS result omitted the resok arm.");
            return new OpenNfsV3AccessResult(
                status,
                MapPostOperationAttributes(resok.obj_attributes, "ACCESS3res.resok.obj_attributes"),
                (OpenNfsV3AccessMask)ReadRequiredUInt32(resok.access, "ACCESS3res.resok.access"));
        }

        internal static OpenNfsV3FileSystemStatusResult ReadFileSystemStatusResult(ReadOnlyMemory<byte> encodedReply)
        {
            FSSTAT3res result = DecodePayload(encodedReply, "NFSv3 FSSTAT", FSSTAT3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 FSSTAT"));

            if (status != OpenNfsV3Status.Ok)
            {
                FSSTAT3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 FSSTAT result omitted the resfail arm.");
                return new OpenNfsV3FileSystemStatusResult(
                    status,
                    objectAttributes: MapPostOperationAttributes(resfail.obj_attributes, "FSSTAT3res.resfail.obj_attributes"));
            }

            FSSTAT3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 FSSTAT result omitted the resok arm.");
            return new OpenNfsV3FileSystemStatusResult(
                status,
                MapPostOperationAttributes(resok.obj_attributes, "FSSTAT3res.resok.obj_attributes"),
                ReadRequiredSize(resok.tbytes, "FSSTAT3res.resok.tbytes"),
                ReadRequiredSize(resok.fbytes, "FSSTAT3res.resok.fbytes"),
                ReadRequiredSize(resok.abytes, "FSSTAT3res.resok.abytes"),
                ReadRequiredSize(resok.tfiles, "FSSTAT3res.resok.tfiles"),
                ReadRequiredSize(resok.ffiles, "FSSTAT3res.resok.ffiles"),
                ReadRequiredSize(resok.afiles, "FSSTAT3res.resok.afiles"),
                ReadRequiredUInt32(resok.invarsec, "FSSTAT3res.resok.invarsec"));
        }

        internal static OpenNfsV3FileSystemInfoResult ReadFileSystemInfoResult(ReadOnlyMemory<byte> encodedReply)
        {
            FSINFO3res result = DecodePayload(encodedReply, "NFSv3 FSINFO", FSINFO3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 FSINFO"));

            if (status != OpenNfsV3Status.Ok)
            {
                FSINFO3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 FSINFO result omitted the resfail arm.");
                return new OpenNfsV3FileSystemInfoResult(
                    status,
                    objectAttributes: MapPostOperationAttributes(resfail.obj_attributes, "FSINFO3res.resfail.obj_attributes"));
            }

            FSINFO3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 FSINFO result omitted the resok arm.");
            return new OpenNfsV3FileSystemInfoResult(
                status,
                MapPostOperationAttributes(resok.obj_attributes, "FSINFO3res.resok.obj_attributes"),
                ReadRequiredUInt32(resok.rtmax, "FSINFO3res.resok.rtmax"),
                ReadRequiredUInt32(resok.rtpref, "FSINFO3res.resok.rtpref"),
                ReadRequiredUInt32(resok.rtmult, "FSINFO3res.resok.rtmult"),
                ReadRequiredUInt32(resok.wtmax, "FSINFO3res.resok.wtmax"),
                ReadRequiredUInt32(resok.wtpref, "FSINFO3res.resok.wtpref"),
                ReadRequiredUInt32(resok.wtmult, "FSINFO3res.resok.wtmult"),
                ReadRequiredUInt32(resok.dtpref, "FSINFO3res.resok.dtpref"),
                ReadRequiredSize(resok.maxfilesize, "FSINFO3res.resok.maxfilesize"),
                MapTime(resok.time_delta, "FSINFO3res.resok.time_delta"),
                (OpenNfsV3FileSystemProperties)ReadRequiredUInt32(resok.properties, "FSINFO3res.resok.properties"));
        }

        internal static OpenNfsV3PathConfigurationResult ReadPathConfigurationResult(ReadOnlyMemory<byte> encodedReply)
        {
            PATHCONF3res result = DecodePayload(encodedReply, "NFSv3 PATHCONF", PATHCONF3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 PATHCONF"));

            if (status != OpenNfsV3Status.Ok)
            {
                PATHCONF3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 PATHCONF result omitted the resfail arm.");
                return new OpenNfsV3PathConfigurationResult(
                    status,
                    objectAttributes: MapPostOperationAttributes(resfail.obj_attributes, "PATHCONF3res.resfail.obj_attributes"));
            }

            PATHCONF3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 PATHCONF result omitted the resok arm.");
            return new OpenNfsV3PathConfigurationResult(
                status,
                MapPostOperationAttributes(resok.obj_attributes, "PATHCONF3res.resok.obj_attributes"),
                ReadRequiredUInt32(resok.linkmax, "PATHCONF3res.resok.linkmax"),
                ReadRequiredUInt32(resok.name_max, "PATHCONF3res.resok.name_max"),
                resok.no_trunc,
                resok.chown_restricted,
                resok.case_insensitive,
                resok.case_preserving);
        }
    }
}
