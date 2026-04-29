namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// ACL permission bits surfaced through the public server capability seam.
    /// </summary>
    [Flags]
    public enum NfsAclPermissionMask : uint
    {
        /// <summary>
        /// No permissions are granted.
        /// </summary>
        None = 0,

        /// <summary>
        /// Read-data or list-directory permission.
        /// </summary>
        ReadData = 1,

        /// <summary>
        /// Write-data or add-file permission.
        /// </summary>
        WriteData = 2,

        /// <summary>
        /// Append-data or add-subdirectory permission.
        /// </summary>
        AppendData = 4,

        /// <summary>
        /// Read-named-attributes permission.
        /// </summary>
        ReadNamedAttributes = 8,

        /// <summary>
        /// Write-named-attributes permission.
        /// </summary>
        WriteNamedAttributes = 16,

        /// <summary>
        /// Execute permission.
        /// </summary>
        Execute = 32,

        /// <summary>
        /// Delete-child permission.
        /// </summary>
        DeleteChild = 64,

        /// <summary>
        /// Read-attributes permission.
        /// </summary>
        ReadAttributes = 128,

        /// <summary>
        /// Write-attributes permission.
        /// </summary>
        WriteAttributes = 256,

        /// <summary>
        /// Delete permission.
        /// </summary>
        Delete = 65536,

        /// <summary>
        /// Read-ACL permission.
        /// </summary>
        ReadAcl = 131072,

        /// <summary>
        /// Write-ACL permission.
        /// </summary>
        WriteAcl = 262144,

        /// <summary>
        /// Write-owner permission.
        /// </summary>
        WriteOwner = 524288,

        /// <summary>
        /// Synchronize permission.
        /// </summary>
        Synchronize = 1048576,

        /// <summary>
        /// Generic-read permission set.
        /// </summary>
        GenericRead = 1179777,

        /// <summary>
        /// Generic-write permission set.
        /// </summary>
        GenericWrite = 1442054,

        /// <summary>
        /// Generic-execute permission set.
        /// </summary>
        GenericExecute = 1179808,
    }
}
