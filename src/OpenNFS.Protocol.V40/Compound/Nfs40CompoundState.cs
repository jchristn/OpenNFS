namespace OpenNFS.Protocol.V40.Compound
{
    internal sealed class Nfs40CompoundState
    {
        private Nfs40CompoundResolvedHandle? _currentHandle;
        private Nfs40CompoundResolvedHandle? _savedHandle;

        internal bool SaveCurrentHandle()
        {
            if (_currentHandle is null)
            {
                return false;
            }

            _savedHandle = _currentHandle;
            return true;
        }

        internal void SetCurrentHandle(Nfs40CompoundResolvedHandle currentHandle)
        {
            _currentHandle = currentHandle;
        }

        internal bool TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle)
        {
            currentHandle = _currentHandle;
            return currentHandle is not null;
        }

        internal bool TryGetSavedHandle(out Nfs40CompoundResolvedHandle? savedHandle)
        {
            savedHandle = _savedHandle;
            return savedHandle is not null;
        }

        internal bool TryRestoreSavedHandle(out Nfs40CompoundResolvedHandle? restoredHandle)
        {
            restoredHandle = _savedHandle;
            if (restoredHandle is null)
            {
                return false;
            }

            _currentHandle = restoredHandle;
            return true;
        }
    }
}
