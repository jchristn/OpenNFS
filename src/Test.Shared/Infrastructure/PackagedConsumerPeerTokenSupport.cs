namespace Test.Shared.Infrastructure
{
    using System;
    using System.Globalization;

    internal static class PackagedConsumerPeerTokenSupport
    {
        internal static string ReplacePeerTokens(
            string template,
            string sampleHost,
            int sampleMountPort,
            int sampleNfsPort,
            int sampleNfs40Port,
            string knfsdHost,
            int knfsdMountPort,
            int knfsdNfsPort,
            string ganeshaHost,
            int ganeshaNfs40Port)
        {
            return template
                .Replace("__SAMPLE_HOST__", sampleHost, StringComparison.Ordinal)
                .Replace("__SAMPLE_MOUNT_PORT__", sampleMountPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__SAMPLE_NFS_PORT__", sampleNfsPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__SAMPLE_NFS40_PORT__", sampleNfs40Port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__KNFSD_HOST__", knfsdHost, StringComparison.Ordinal)
                .Replace("__KNFSD_MOUNT_PORT__", knfsdMountPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__KNFSD_NFS_PORT__", knfsdNfsPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("__GANESHA_HOST__", ganeshaHost, StringComparison.Ordinal)
                .Replace("__GANESHA_NFS40_PORT__", ganeshaNfs40Port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }
    }
}
