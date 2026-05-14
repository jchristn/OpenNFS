namespace OpenNFS.Protocol.V40.Compound
{
    using System.IO;

    internal static class Nfs40CompoundFileKeySupport
    {
        internal static string BuildOpenFileKey(string exportPath, string sourcePath)
        {
            return exportPath + "|" + Path.GetFullPath(sourcePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
