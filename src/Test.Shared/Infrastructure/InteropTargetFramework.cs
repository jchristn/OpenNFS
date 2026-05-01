namespace Test.Shared.Infrastructure
{
    using System;

    /// <summary>
    /// Resolves the .NET target-framework moniker of the running test process so that helpers that
    /// spawn child processes via <c>dotnet run --project</c> can pass the correct
    /// <c>--framework</c> argument against multi-targeted projects.
    /// </summary>
    internal static class InteropTargetFramework
    {
        /// <summary>
        /// Gets the current TFM moniker (<c>net8.0</c>, <c>net10.0</c>, etc.) for the running process.
        /// </summary>
        internal static string Current
        {
            get
            {
                Version version = Environment.Version;
                return "net" + version.Major + "." + version.Minor;
            }
        }
    }
}
