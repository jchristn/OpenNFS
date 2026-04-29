namespace OpenNFS.Server.Abstractions
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Supplies the export definitions exposed by a server configuration.
    /// </summary>
    public interface INfsExportProvider
    {
        /// <summary>
        /// Resolves the export definitions exposed by the host.
        /// </summary>
        /// <param name="request">Request context for the export-resolution operation.</param>
        /// <returns>The configured export definitions.</returns>
        Task<NfsGetExportsResponse> GetExportsAsync(NfsGetExportsRequest request);
    }
}
