namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Server;

    internal sealed class Nfs3ObjectResolution
    {
        internal Nfs3ObjectResolution(
            nfsstat3 status,
            post_op_attr postOperationAttributes,
            NfsFileHandleTarget? target = null,
            NfsPathInfo? pathInfo = null,
            fattr3? attributes = null)
        {
            ArgumentNullException.ThrowIfNull(postOperationAttributes);

            if (status == nfsstat3.NFS3_OK)
            {
                if (target is null)
                {
                    throw new ArgumentException("Successful NFSv3 object resolution requires a target.", nameof(target));
                }

                if (pathInfo is null)
                {
                    throw new ArgumentException("Successful NFSv3 object resolution requires path information.", nameof(pathInfo));
                }

                if (attributes is null)
                {
                    throw new ArgumentException("Successful NFSv3 object resolution requires synthetic attributes.", nameof(attributes));
                }

                if (!postOperationAttributes.attributes_follow || postOperationAttributes.attributes is null)
                {
                    throw new ArgumentException("Successful NFSv3 object resolution requires post-operation attributes.", nameof(postOperationAttributes));
                }
            }

            Status = status;
            PostOperationAttributes = postOperationAttributes;
            Target = target;
            PathInfo = pathInfo;
            Attributes = attributes;
        }

        internal nfsstat3 Status { get; }

        internal post_op_attr PostOperationAttributes { get; }

        internal NfsFileHandleTarget? Target { get; }

        internal NfsPathInfo? PathInfo { get; }

        internal fattr3? Attributes { get; }
    }
}
