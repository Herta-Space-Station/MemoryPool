using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable ALL

namespace NativeCollections
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ConcurrentIndexPool4096
    {
        private Blocks _blocks;

        public bool TryRent(out int index)
        {
            ref var bitArray = ref Unsafe.As<Blocks, ulong>(ref _blocks);
            do
            {
                var bitArrayValue = InterlockedHelpers.Read(ref bitArray);
                if (bitArrayValue == ulong.MaxValue)
                {
                    index = -1;
                    return false;
                }

                var quotient = BitOperationsHelpers.TrailingZeroCount(~bitArrayValue);
                ref var segment = ref Unsafe.Add(ref bitArray, 1 + quotient);
                do
                {
                    var segmentValue = InterlockedHelpers.Read(ref segment);
                    if (segmentValue == ulong.MaxValue)
                        break;
                    var remainder = BitOperationsHelpers.TrailingZeroCount(~segmentValue);
                    var newSegmentValue = segmentValue | (1UL << remainder);
                    if (InterlockedHelpers.CompareExchange(ref segment, newSegmentValue, segmentValue) == segmentValue)
                    {
                        if (newSegmentValue == ulong.MaxValue)
                        {
                            ulong newBitArrayValue;
                            do
                            {
                                bitArrayValue = InterlockedHelpers.Read(ref bitArray);
                                newBitArrayValue = bitArrayValue | (1UL << quotient);
                            } while (InterlockedHelpers.CompareExchange(ref bitArray, newBitArrayValue, bitArrayValue) != bitArrayValue);
                        }

                        index = (quotient << 6) + remainder;
                        return true;
                    }
                } while (true);
            } while (true);
        }

        public void Return(int index)
        {
            ThrowHelpers.ThrowIfGreaterThanOrEqual((uint)index, 4096U, nameof(index));
            ref var bitArray = ref Unsafe.As<Blocks, ulong>(ref _blocks);
            var quotient = index >> 6;
            var remainder = index & 63;
            ref var segment = ref Unsafe.Add(ref bitArray, 1 + quotient);
            do
            {
                var segmentValue = InterlockedHelpers.Read(ref segment);
                var newSegmentValue = segmentValue & ~(1UL << remainder);
                if (InterlockedHelpers.CompareExchange(ref segment, newSegmentValue, segmentValue) == segmentValue)
                {
                    if (segmentValue == ulong.MaxValue)
                    {
                        ulong bitArrayValue;
                        ulong newBitArrayValue;
                        do
                        {
                            bitArrayValue = InterlockedHelpers.Read(ref bitArray);
                            newBitArrayValue = bitArrayValue & ~(1UL << quotient);
                        } while (InterlockedHelpers.CompareExchange(ref bitArray, newBitArrayValue, bitArrayValue) != bitArrayValue);
                    }

                    break;
                }
            } while (true);
        }

        [StructLayout(LayoutKind.Sequential, Size = 520)]
        private struct Blocks
        {
            private ulong _padding;
        }
    }
}