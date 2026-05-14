namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Client.Compound;

    internal static class OpenNfsCompoundPayloadCodec
    {
        internal static byte[] Encode(OpenNfsCompoundRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Encode(request.Tag, ResolveMinorVersion(request.ProtocolVersion), request.Operations);
        }

        internal static byte[] Encode(OpenNfsCompoundPlan plan)
        {
            ArgumentNullException.ThrowIfNull(plan);
            return Encode(plan.Tag, plan.MinorVersion, plan.Operations);
        }

        internal static byte[] Encode(
            string tag,
            uint minorVersion,
            IReadOnlyList<OpenNfsCompoundOperation> operations)
        {
            ArgumentNullException.ThrowIfNull(tag);
            ArgumentNullException.ThrowIfNull(operations);

            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteString(tag);
            writer.WriteUInt32(minorVersion);
            writer.WriteUInt32((uint)operations.Count);

            for (int index = 0; index < operations.Count; index++)
            {
                OpenNfsCompoundOperation operation = operations[index];
                if (operation.OperationNumber > int.MaxValue)
                {
                    throw new InvalidOperationException(
                        "The COMPOUND operation number " + operation.OperationNumber + " exceeds the signed 32-bit XDR enum range.");
                }

                writer.WriteInt32((int)operation.OperationNumber);
                writer.WriteBytes(operation.OperationPayload.Span);
            }

            return writer.ToArray();
        }

        internal static uint ResolveMinorVersion(OpenNfsProtocolVersion protocolVersion)
        {
            return protocolVersion switch
            {
                OpenNfsProtocolVersion.Nfs40 => 0U,
                OpenNfsProtocolVersion.Nfs41 => 1U,
                OpenNfsProtocolVersion.Nfs42 => 2U,
                _ => throw new InvalidOperationException(
                    "The requested NFS protocol version '" + protocolVersion.ToString() + "' is not supported by the COMPOUND payload encoder."),
            };
        }
    }
}
