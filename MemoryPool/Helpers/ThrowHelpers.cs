using System;
using System.Runtime.CompilerServices;

#pragma warning disable CA2208
#pragma warning disable CS8632

// ReSharper disable ALL

namespace NativeCollections
{
    /// <summary>
    ///     Throw helpers
    /// </summary>
    internal static class ThrowHelpers
    {
        /// <summary>
        ///     Throws an <see cref="ArgumentOutOfRangeException" /> if <paramref name="value" /> is greater than or equal
        ///     <paramref name="other" />.
        /// </summary>
        /// <param name="value">The argument to validate as less than <paramref name="other" />.</param>
        /// <param name="other">The value to compare with <paramref name="value" />.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value" /> corresponds.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfGreaterThanOrEqual<T>(T value, T other, string? paramName) where T : unmanaged, IComparable<T>
        {
            if (value.CompareTo(other) >= 0)
                throw new ArgumentOutOfRangeException(paramName, value, "MustBeLess");
        }
    }
}