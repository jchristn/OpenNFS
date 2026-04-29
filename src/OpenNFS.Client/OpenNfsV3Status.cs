#pragma warning disable CS1591
namespace OpenNFS.Client
{
    /// <summary>
    /// NFSv3 procedure status codes surfaced by the grouped client reply models.
    /// </summary>
    public enum OpenNfsV3Status
    {
        Ok = 0,
        PermissionDenied = 1,
        NoEntry = 2,
        Io = 5,
        NoSuchDeviceOrAddress = 6,
        AccessDenied = 13,
        AlreadyExists = 17,
        CrossDevice = 18,
        NoSuchDevice = 19,
        NotDirectory = 20,
        IsDirectory = 21,
        InvalidArgument = 22,
        FileTooLarge = 27,
        NoSpace = 28,
        ReadOnlyFileSystem = 30,
        TooManyLinks = 31,
        NameTooLong = 63,
        NotEmpty = 66,
        QuotaExceeded = 69,
        Stale = 70,
        Remote = 71,
        BadHandle = 10001,
        NotSynchronized = 10002,
        BadCookie = 10003,
        NotSupported = 10004,
        TooSmall = 10005,
        ServerFault = 10006,
        BadType = 10007,
        Jukebox = 10008,
    }
}
#pragma warning restore CS1591
