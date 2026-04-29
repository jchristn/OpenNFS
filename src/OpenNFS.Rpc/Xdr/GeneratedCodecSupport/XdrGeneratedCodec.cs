namespace OpenNFS.Rpc.Xdr
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provides shared helper methods for generated XDR codec implementations.
    /// </summary>
    public static class XdrGeneratedCodec
    {
        /// <summary>
        /// Ensures a required reference-typed XDR member is present during encoding.
        /// </summary>
        /// <typeparam name="T">The reference type being validated.</typeparam>
        /// <param name="value">The candidate member value.</param>
        /// <param name="memberName">The fully qualified member name used for error context.</param>
        /// <returns>The validated member value.</returns>
        public static T RequireReference<T>(T? value, string memberName)
            where T : class
        {
            if (value is null)
            {
                throw new InvalidOperationException("The required XDR member '" + memberName + "' was null during encoding.");
            }

            return value;
        }

        /// <summary>
        /// Ensures a required nullable value-typed XDR member is present during encoding.
        /// </summary>
        /// <typeparam name="T">The value type being validated.</typeparam>
        /// <param name="value">The candidate member value.</param>
        /// <param name="memberName">The fully qualified member name used for error context.</param>
        /// <returns>The validated member value.</returns>
        public static T RequireValue<T>(T? value, string memberName)
            where T : struct
        {
            if (!value.HasValue)
            {
                throw new InvalidOperationException("The required XDR member '" + memberName + "' was null during encoding.");
            }

            return value.Value;
        }

        /// <summary>
        /// Ensures a fixed-length XDR array is present and matches its expected element count.
        /// </summary>
        /// <typeparam name="T">The array element type.</typeparam>
        /// <param name="value">The candidate array value.</param>
        /// <param name="expectedLength">The required element count.</param>
        /// <param name="memberName">The fully qualified member name used for error context.</param>
        /// <returns>The validated array value.</returns>
        public static T[] RequireFixedLength<T>(T[]? value, int expectedLength, string memberName)
        {
            if (value is null)
            {
                throw new InvalidOperationException("The required XDR member '" + memberName + "' was null during encoding.");
            }

            if (value.Length != expectedLength)
            {
                throw new InvalidOperationException(
                    "The fixed-length XDR member '" + memberName + "' expected " + expectedLength + " element(s), but received " + value.Length + ".");
            }

            return value;
        }

        /// <summary>
        /// Materializes an XDR array result as a concrete CLR array.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="values">The decoded values.</param>
        /// <returns>The materialized array.</returns>
        public static T[] ToArray<T>(IReadOnlyList<T> values)
        {
            ArgumentNullException.ThrowIfNull(values);

            if (values is T[] typedArray)
            {
                return typedArray;
            }

            T[] array = new T[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                array[index] = values[index];
            }

            return array;
        }
    }
}
