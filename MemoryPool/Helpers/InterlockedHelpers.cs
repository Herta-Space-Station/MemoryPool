using System;
using System.Runtime.CompilerServices;
using System.Threading;

// ReSharper disable ALL

namespace NativeCollections
{
    /// <summary>
    ///     Interlocked helpers
    /// </summary>
    internal static class InterlockedHelpers
    {
        /// <summary>Returns a 64-bit unsigned value, loaded as an atomic operation.</summary>
        /// <param name="location">The 64-bit value to be loaded.</param>
        /// <returns>The loaded value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Read(ref ulong location) => CompareExchange(ref Unsafe.AsRef(in location), 0, 0);

        /// <summary>Compares two 64-bit unsigned integers for equality and, if they are equal, replaces the first value.</summary>
        /// <param name="location">
        ///     The destination, whose value is compared with <paramref name="comparand" /> and possibly
        ///     replaced.
        /// </param>
        /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
        /// <param name="comparand">The value that is compared to the value at <paramref name="location" />.</param>
        /// <returns>The original value in <paramref name="location" />.</returns>
        /// <exception cref="NullReferenceException">The address of <paramref name="location" /> is a null pointer.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong CompareExchange(ref ulong location, ulong value, ulong comparand) => (ulong)Interlocked.CompareExchange(ref Unsafe.As<ulong, long>(ref location), (long)value, (long)comparand);
    }
}