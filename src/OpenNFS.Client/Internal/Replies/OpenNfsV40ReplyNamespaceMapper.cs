namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyAttributeMapper;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyValueReader;

    internal static class OpenNfsV40ReplyNamespaceMapper
    {
        internal static IReadOnlyList<OpenNfsV40SecurityFlavorInfo> MapSecurityFlavors(secinfo4[]? values)
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

        internal static OpenNfsV40SecurityFlavorInfo MapSecurityFlavor(secinfo4 value, string fieldName)
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

        internal static IReadOnlyList<OpenNfsV40DirectoryEntry> MapDirectoryEntries(entry4? entries)
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
    }
}
