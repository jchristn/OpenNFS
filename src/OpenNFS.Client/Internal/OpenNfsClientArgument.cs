namespace OpenNFS.Client.Internal
{
    using System;

    internal static class OpenNfsClientArgument
    {
        internal static byte[] RequireBytes(byte[]? value, string parameterName, bool allowEmpty)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (!allowEmpty && value.Length < 1)
            {
                throw new ArgumentException("The byte payload cannot be empty.", parameterName);
            }

            return value.AsSpan().ToArray();
        }

        internal static byte[] RequireFixedBytes(byte[]? value, int expectedLength, string parameterName)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (value.Length != expectedLength)
            {
                throw new ArgumentException("The byte payload must contain exactly " + expectedLength + " byte(s).", parameterName);
            }

            return value.AsSpan().ToArray();
        }

        /// <summary>
        /// Validates a single directory-entry name. Names are passed verbatim (leading, trailing, and repeated spaces are kept);
        /// only null, empty, and whitespace-only names are rejected.
        /// </summary>
        internal static string RequireEntryName(string? value, string parameterName)
        {
            ArgumentNullException.ThrowIfNull(value, parameterName);

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The entry name cannot be empty or whitespace-only.", parameterName);
            }

            return value;
        }

        internal static string RequireText(string? value, string parameterName, bool allowEmpty = false)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (!allowEmpty && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The text value cannot be empty or whitespace.", parameterName);
            }

            return value;
        }
    }
}
