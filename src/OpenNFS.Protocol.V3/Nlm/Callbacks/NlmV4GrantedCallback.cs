namespace OpenNFS.Protocol.V3.Nlm.Callbacks
{
    using System;

    internal sealed class NlmV4GrantedCallback
    {
        private readonly byte[] _Cookie;
        private readonly byte[] _FileHandle;
        private readonly byte[] _OwnerHandle;

        internal NlmV4GrantedCallback(
            string callerName,
            ReadOnlyMemory<byte> cookie,
            ReadOnlyMemory<byte> fileHandle,
            ReadOnlyMemory<byte> ownerHandle,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool exclusive)
        {
            if (string.IsNullOrWhiteSpace(callerName))
            {
                throw new ArgumentException("The granted callback must contain a non-empty caller name.", nameof(callerName));
            }

            if (fileHandle.Length < 1)
            {
                throw new ArgumentException("The granted callback must contain a non-empty filehandle payload.", nameof(fileHandle));
            }

            if (ownerHandle.Length < 1)
            {
                throw new ArgumentException("The granted callback must contain a non-empty owner-handle payload.", nameof(ownerHandle));
            }

            CallerName = callerName;
            _Cookie = cookie.ToArray();
            _FileHandle = fileHandle.ToArray();
            _OwnerHandle = ownerHandle.ToArray();
            OwnerProcessId = ownerProcessId;
            Offset = offset;
            Length = length;
            Exclusive = exclusive;
        }

        internal string CallerName { get; }

        internal ReadOnlyMemory<byte> Cookie => _Cookie;

        internal ReadOnlyMemory<byte> FileHandle => _FileHandle;

        internal ReadOnlyMemory<byte> OwnerHandle => _OwnerHandle;

        internal int OwnerProcessId { get; }

        internal ulong Offset { get; }

        internal ulong Length { get; }

        internal bool Exclusive { get; }
    }
}
