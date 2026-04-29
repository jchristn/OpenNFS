#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using OpenNFS.Protocol.V40.Generated;

    /// <summary>
    /// Public NFSv4.0 attribute identifiers that can be requested through grouped client APIs.
    /// </summary>
    public enum OpenNfsV40AttributeKind
    {
        SupportedAttributes = (int)Nfs40Constants.FATTR4_SUPPORTED_ATTRS,
        Type = (int)Nfs40Constants.FATTR4_TYPE,
        Acl = (int)Nfs40Constants.FATTR4_ACL,
        AclSupport = (int)Nfs40Constants.FATTR4_ACLSUPPORT,
        Change = (int)Nfs40Constants.FATTR4_CHANGE,
        Size = (int)Nfs40Constants.FATTR4_SIZE,
        FileHandle = (int)Nfs40Constants.FATTR4_FILEHANDLE,
        Owner = (int)Nfs40Constants.FATTR4_OWNER,
        OwnerGroup = (int)Nfs40Constants.FATTR4_OWNER_GROUP,
    }
}
#pragma warning restore CS1591
