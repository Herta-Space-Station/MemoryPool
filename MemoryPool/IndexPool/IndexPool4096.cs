using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable ALL

namespace NativeCollections
{
    [StructLayout(LayoutKind.Sequential)]
    public struct IndexPool4096
    {
        private Blocks _blocks;

        public bool TryRent(out int index)
        {
            ref var bitArray = ref Unsafe.As<Blocks, ulong>(ref _blocks);
            if (bitArray == ulong.MaxValue)
            {
                index = -1;
                return false;
            }

            var quotient = BitOperationsHelpers.TrailingZeroCount(~bitArray);
            ref var segment = ref Unsafe.Add(ref bitArray, 1 + quotient);
            var remainder = BitOperationsHelpers.TrailingZeroCount(~segment);
            segment |= 1UL << remainder;
            if (segment == ulong.MaxValue)
                bitArray |= 1UL << quotient;
            index = (quotient << 6) + remainder;
            return true;
        }

        public void Return(int index)
        {
            ThrowHelpers.ThrowIfGreaterThanOrEqual((uint)index, 4096U, nameof(index));
            ref var bitArray = ref Unsafe.As<Blocks, ulong>(ref _blocks);
            var quotient = index >> 6;
            var remainder = index & 63;
            ref var segment = ref Unsafe.Add(ref bitArray, 1 + quotient);
            segment &= ~(1UL << remainder);
            if (segment == ulong.MaxValue)
                bitArray &= ~(1UL << quotient);
        }

        [StructLayout(LayoutKind.Sequential, Size = 520)]
        private struct Blocks
        {
            private ulong _padding;
        }
    }
}