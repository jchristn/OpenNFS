namespace OpenNFS.Server.Internal
{
    using System;

    /// <summary>
    /// Path-utility helpers that accept both <c>/</c> and <c>\</c> as directory separators regardless
    /// of the host platform.
    /// </summary>
    /// <remarks>
    /// The standard <see cref="System.IO.Path"/> helpers honor the platform's separator: on Linux
    /// they recognize only <c>/</c>; on Windows they recognize <c>\</c> and <c>/</c>. NFS source
    /// paths flow through the server from arbitrary file-system contracts, including in-memory
    /// fixtures that may use one platform's separator under a host running on the other. The server
    /// must navigate (combine, split, identify boundaries) without losing fidelity, so this helper
    /// canonicalizes both characters as separators while preserving the parent's separator on
    /// combine.
    /// </remarks>
    internal static class NfsSourcePath
    {
        /// <summary>
        /// Returns true when <paramref name="value"/> is either <c>/</c> or <c>\</c>.
        /// </summary>
        /// <param name="value">The character.</param>
        /// <returns>The result.</returns>
        internal static bool IsSeparator(char value)
        {
            return value == '/' || value == '\\';
        }

        /// <summary>
        /// Returns the parent directory portion of <paramref name="sourcePath"/>, or <c>null</c> when
        /// the path has no parent. Recognizes both separator characters.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <returns>The parent, or null.</returns>
        internal static string? GetDirectoryName(string? sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                return null;
            }

            int trimEnd = sourcePath.Length;
            while (trimEnd > 0 && IsSeparator(sourcePath[trimEnd - 1]))
            {
                trimEnd--;
            }

            for (int index = trimEnd - 1; index >= 0; index--)
            {
                if (IsSeparator(sourcePath[index]))
                {
                    if (index == 0)
                    {
                        return sourcePath.Substring(0, 1);
                    }

                    return sourcePath.Substring(0, index);
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the trailing entry name of <paramref name="sourcePath"/>. Recognizes both separator
        /// characters.
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <returns>The entry name.</returns>
        internal static string GetFileName(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                return string.Empty;
            }

            int trimEnd = sourcePath.Length;
            while (trimEnd > 0 && IsSeparator(sourcePath[trimEnd - 1]))
            {
                trimEnd--;
            }

            for (int index = trimEnd - 1; index >= 0; index--)
            {
                if (IsSeparator(sourcePath[index]))
                {
                    return sourcePath.Substring(index + 1, trimEnd - index - 1);
                }
            }

            return sourcePath.Substring(0, trimEnd);
        }

        /// <summary>
        /// Joins <paramref name="parentDirectory"/> and <paramref name="entryName"/> using the
        /// parent's existing separator (or <c>/</c> when the parent contains none) so the result
        /// preserves a consistent separator style.
        /// </summary>
        /// <param name="parentDirectory">The parent directory.</param>
        /// <param name="entryName">The entry name.</param>
        /// <returns>The combined path.</returns>
        internal static string Combine(string parentDirectory, string entryName)
        {
            ArgumentNullException.ThrowIfNull(parentDirectory);
            ArgumentNullException.ThrowIfNull(entryName);

            int trimEnd = parentDirectory.Length;
            while (trimEnd > 0 && IsSeparator(parentDirectory[trimEnd - 1]))
            {
                trimEnd--;
            }

            string trimmedParent = trimEnd == parentDirectory.Length
                ? parentDirectory
                : parentDirectory.Substring(0, trimEnd);

            string trimmedEntry = entryName;
            int entryStart = 0;
            while (entryStart < trimmedEntry.Length && IsSeparator(trimmedEntry[entryStart]))
            {
                entryStart++;
            }

            if (entryStart > 0)
            {
                trimmedEntry = trimmedEntry.Substring(entryStart);
            }

            if (trimmedParent.Length == 0)
            {
                return trimmedEntry;
            }

            char separator = '/';
            for (int index = 0; index < trimmedParent.Length; index++)
            {
                if (trimmedParent[index] == '\\')
                {
                    separator = '\\';
                    break;
                }

                if (trimmedParent[index] == '/')
                {
                    separator = '/';
                    break;
                }
            }

            return trimmedParent + separator + trimmedEntry;
        }
    }
}
