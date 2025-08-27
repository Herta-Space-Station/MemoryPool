using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

// ReSharper disable ALL

namespace NativeCollections
{
    /// <summary>
    ///     An allocator that is fast like a linear allocator, is thread-safe, and automatically invalidates
    ///     all allocations made from it, when call "<see cref="Rewind()" />" by the user.
    /// </summary>
    public sealed class RewindableAllocator
    {
        /// <summary>
        ///     Represents a union of current position and allocation count in a memory block.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct Union
        {
            /// <summary>
            ///     The combined value storing both current position and allocation count.
            /// </summary>
            public long Value;

            /// <summary>
            ///     Number of bits used to store current position in a block to give out memory.
            ///     This limits the maximum block size to 1TB (2^40).
            /// </summary>
            private const int UNION_CURRENT_BITS = 40;

            /// <summary>
            ///     Offset of current position in Value
            /// </summary>
            private const int UNION_CURRENT_OFFSET = 0;

            /// <summary>
            ///     Number of bits used to store the allocation count in a block
            /// </summary>
            private const long UNION_CURRENT_MASK = (1L << UNION_CURRENT_BITS) - 1;

            /// <summary>
            ///     Number of bits used to store allocation count in a block.
            ///     This limits the maximum number of allocations per block to 16 millions (2^24)
            /// </summary>
            private const int UNION_ALLOC_COUNT_BITS = 24;

            /// <summary>
            ///     Offset of allocation count in Value
            /// </summary>
            private const int UNION_ALLOC_COUNT_OFFSET = UNION_CURRENT_OFFSET + UNION_CURRENT_BITS;

            /// <summary>
            ///     Mask of allocation count in Value
            /// </summary>
            private const long UNION_ALLOC_COUNT_MASK = (1L << UNION_ALLOC_COUNT_BITS) - 1;

            /// <summary>
            ///     Current position in a block to give out memory
            /// </summary>
            public long Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (Value >> UNION_CURRENT_OFFSET) & UNION_CURRENT_MASK;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set
                {
                    Value &= ~(UNION_CURRENT_MASK << UNION_CURRENT_OFFSET);
                    Value |= (value & UNION_CURRENT_MASK) << UNION_CURRENT_OFFSET;
                }
            }

            /// <summary>
            ///     The number of allocations in a block
            /// </summary>
            public long AllocCount
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (Value >> UNION_ALLOC_COUNT_OFFSET) & UNION_ALLOC_COUNT_MASK;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set
                {
                    Value &= ~(UNION_ALLOC_COUNT_MASK << UNION_ALLOC_COUNT_OFFSET);
                    Value |= (value & UNION_ALLOC_COUNT_MASK) << UNION_ALLOC_COUNT_OFFSET;
                }
            }
        }

        /// <summary>
        ///     Represents a memory block allocated by the allocator.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private sealed class MemoryBlock : IDisposable
        {
            /// <summary>
            ///     Can't align any coarser than this many bytes
            /// </summary>
            private const int MEMORY_BLOCK_MAXIMUM_ALIGNMENT = 16384;

            public byte[] Array;

            public nint Buffer;

            private GCHandle _gcHandle;

            /// <summary>
            ///     Pointer to the allocated memory.
            /// </summary>
            public nint Pointer;

            /// <summary>
            ///     Size of the allocated memory in bytes.
            /// </summary>
            public long Bytes;

            /// <summary>
            ///     Union containing current position and allocation count.
            /// </summary>
            public Union Union;

            /// <summary>
            ///     Initializes a new memory block with the specified size.
            /// </summary>
            /// <param name="bytes">Size of the memory block in bytes.</param>
            public MemoryBlock(long bytes)
            {
                var byteOffset = (nuint)MEMORY_BLOCK_MAXIMUM_ALIGNMENT - 1;
                Array = new byte[(nint)(bytes + (uint)byteOffset)];
                _gcHandle = GCHandle.Alloc(Array, GCHandleType.Pinned);
                var buffer = Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref ArrayHelpers.GetArrayDataReference(Array));
                Buffer = buffer;
                Pointer = (buffer + (MEMORY_BLOCK_MAXIMUM_ALIGNMENT - 1)) & (nint)(~(MEMORY_BLOCK_MAXIMUM_ALIGNMENT - 1));
                Bytes = bytes;
                Union = default;
            }

            /// <summary>
            ///     Resets the memory block to its initial state.
            /// </summary>
            public void Rewind() => Union = default;

            /// <summary>
            ///     Releases all resources used by the memory block.
            /// </summary>
            public void Dispose()
            {
                _gcHandle.Free();
                Pointer = 0;
                Bytes = 0;
                Union = default;
            }

            /// <summary>
            ///     Checks if the specified pointer is within this memory block.
            /// </summary>
            /// <param name="ptr">Pointer to check.</param>
            /// <returns>True if the pointer is within this block, false otherwise.</returns>
            public bool Contains(nint ptr) => ptr >= Pointer && ptr < Pointer + Union.Current;
        }

        /// <summary>
        ///     Log2 of Maximum memory block size.  Cannot exceed <see cref="Union.UNION_CURRENT_BITS" />.
        /// </summary>
        private const int MEMORY_BLOCK_LOG2_MAX_MEMORY_BLOCK_SIZE = 26;

        /// <summary>
        ///     Maximum memory block size.  Can exceed maximum memory block size if user requested more. 64MB
        /// </summary>
        private const long MEMORY_BLOCK_MAX_MEMORY_BLOCK_SIZE = 1L << MEMORY_BLOCK_LOG2_MAX_MEMORY_BLOCK_SIZE;

        /// <summary>
        ///     Minimum memory block size, 128KB.
        /// </summary>
        private const long MEMORY_BLOCK_MIN_MEMORY_BLOCK_SIZE = 128 * 1024;

        /// <summary>
        ///     Maximum number of memory blocks.
        /// </summary>
        private const int MEMORY_BLOCK_MAX_NUM_BLOCKS = 64;

        /// <summary>
        ///     Spin lock used for thread synchronization.
        /// </summary>
        private SpinLock _spinLock;

        /// <summary>
        ///     Array of memory blocks managed by this allocator.
        /// </summary>
        private MemoryBlock[] _blocks;

        /// <summary>
        ///     Index of the highest block that has memory available for allocation.
        /// </summary>
        private int _last;

        /// <summary>
        ///     Index of the highest block that was actually used for allocation since last rewind.
        /// </summary>
        private int _used;

        /// <summary>
        ///     Flag indicating whether individual block freeing is enabled.
        /// </summary>
        private byte _enableBlockFree;

        /// <summary>
        ///     Flag indicating whether the maximum block size has been reached.
        /// </summary>
        private byte _reachMaxBlockSize;

        /// <summary>
        ///     Version number incremented on each rewind.
        /// </summary>
        private int _version;

        /// <summary>
        ///     Structure
        /// </summary>
        /// <param name="initialSizeInBytes">The initial capacity of the allocator, in bytes</param>
        /// <param name="enableBlockFree">A flag indicating if allocator enables individual block free</param>
        public RewindableAllocator(int initialSizeInBytes, bool enableBlockFree = false)
        {
            _spinLock = default;
            _blocks = new MemoryBlock[MEMORY_BLOCK_MAX_NUM_BLOCKS];
            var blockSize = Math.Min(Math.Max(initialSizeInBytes, MEMORY_BLOCK_MIN_MEMORY_BLOCK_SIZE), MEMORY_BLOCK_MAX_MEMORY_BLOCK_SIZE);
            _blocks[0] = new MemoryBlock(blockSize);
            _last = _used = 0;
            _enableBlockFree = enableBlockFree ? (byte)1 : (byte)0;
            _reachMaxBlockSize = initialSizeInBytes >= MEMORY_BLOCK_MAX_MEMORY_BLOCK_SIZE ? (byte)1 : (byte)0;
            _version = 0;
        }

        /// <summary>
        ///     Property to get and set enable block free flag, a flag indicating whether the allocator should enable individual
        ///     block to be freed.
        /// </summary>
        public bool EnableBlockFree
        {
            get => _enableBlockFree != 0;
            set => _enableBlockFree = value ? (byte)1 : (byte)0;
        }

        /// <summary>
        ///     Retrieves the number of memory blocks that the allocator has requested from the system.
        /// </summary>
        public int BlocksAllocated => _last + 1;

        /// <summary>
        ///     Retrieves the size of the initial memory block, as requested in the Initialize function.
        /// </summary>
        public int InitialSizeInBytes => (int)_blocks[0].Bytes;

        /// <summary>
        ///     Version number incremented on each rewind.
        /// </summary>
        public int Version => _version;

        /// <summary>
        ///     Rewind the allocator; invalidate all allocations made from it, and potentially also free memory blocks
        ///     it has allocated from the system.
        /// </summary>
        public void Rewind()
        {
            ++_version;
            while (_last > _used)
                _blocks[_last--].Dispose();
            while (_used > 0)
                _blocks[_used--].Rewind();
            _blocks[0].Rewind();
        }

        /// <summary>
        ///     Dispose the allocator. This must be called to free the memory blocks that were allocated from the system.
        /// </summary>
        public void Dispose()
        {
            _used = 0;
            Rewind();
            _blocks[0].Dispose();
            _last = _used = 0;
        }

        /// <summary>
        ///     Attempts to allocate memory from existing blocks.
        /// </summary>
        /// <param name="block">The block to allocate memory for.</param>
        /// <param name="startIndex">First block index to try.</param>
        /// <param name="lastIndex">Last block index to try.</param>
        /// <param name="alignedSize">Size of allocation after alignment.</param>
        /// <param name="alignmentMask">Mask used for alignment calculations.</param>
        /// <returns>0 if successful, -1 otherwise.</returns>
        private int TryAllocate(ref Block block, int startIndex, int lastIndex, long alignedSize, long alignmentMask)
        {
            for (var best = startIndex; best <= lastIndex; ++best)
            {
                Union oldUnion;
                Union readUnion = default;
                long begin;
                var skip = false;
                readUnion.Value = Interlocked.Read(ref _blocks[best].Union.Value);
                do
                {
                    begin = (readUnion.Current + alignmentMask) & ~alignmentMask;
                    if (begin + block.Bytes > _blocks[best].Bytes)
                    {
                        skip = true;
                        break;
                    }

                    oldUnion = readUnion;
                    Union newUnion = default;
                    newUnion.Current = begin + alignedSize > _blocks[best].Bytes ? _blocks[best].Bytes : begin + alignedSize;
                    newUnion.AllocCount = readUnion.AllocCount + 1;
                    readUnion.Value = Interlocked.CompareExchange(ref _blocks[best].Union.Value, newUnion.Value, oldUnion.Value);
                } while (readUnion.Value != oldUnion.Value);

                if (skip)
                    continue;

                ref var targetBlock = ref _blocks[best];
                block.Range.Pointer = new ArraySegment<byte>(targetBlock.Array, (int)(targetBlock.Pointer + begin - targetBlock.Buffer), block.Range.Items);
                block.AllocatedItems = block.Range.Items;
                Interlocked.MemoryBarrier();
                int oldUsed;
                int newUsed;
                var readUsed = _used;
                do
                {
                    oldUsed = readUsed;
                    newUsed = best > oldUsed ? best : oldUsed;
                    readUsed = Interlocked.CompareExchange(ref _used, newUsed, oldUsed);
                } while (newUsed != oldUsed);

                return 0;
            }

            return -1;
        }

        /// <summary>
        ///     Try to allocate, free, or reallocate a block of memory. This is a private function, and
        ///     is not generally called by the user.
        /// </summary>
        /// <param name="block">The memory block to allocate, free, or reallocate</param>
        /// <returns>0 if successful. Otherwise, returns the error code from the allocator function.</returns>
        private int Try(ref Block block)
        {
            var alignment = Math.Max(128, block.Alignment);
            var extra = alignment != 128 ? 1 : 0;
            const int cacheLineMask = 128 - 1;
            if (extra == 1)
                alignment = (alignment + cacheLineMask) & ~cacheLineMask;
            var mask = alignment - 1L;
            var size = (block.Bytes + extra * alignment + mask) & ~mask;
            var last = _last;
            var error = TryAllocate(ref block, 0, _last, size, mask);
            if (error == 0)
                return error;
            var lockTaken = false;
            _spinLock.Enter(ref lockTaken);

            try
            {
                error = TryAllocate(ref block, last, _last, size, mask);
                if (error == 0)
                    return error;
                var bytes = _reachMaxBlockSize == 0 ? _blocks[_last].Bytes << 1 : _blocks[_last].Bytes + MEMORY_BLOCK_MAX_MEMORY_BLOCK_SIZE;
                bytes = Math.Max(bytes, size);
                _reachMaxBlockSize = bytes >= MEMORY_BLOCK_MAX_MEMORY_BLOCK_SIZE ? (byte)1 : (byte)0;
                _blocks[_last + 1] = new MemoryBlock(bytes);
                Interlocked.Increment(ref _last);
                error = TryAllocate(ref block, _last, _last, size, mask);
            }
            finally
            {
                if (lockTaken)
                    _spinLock.Exit();
            }

            return error;
        }

        /// <summary>
        ///     Attempts to free memory at the specified pointer.
        /// </summary>
        /// <param name="ptr">Pointer to memory to free.</param>
        /// <returns>Always returns 0.</returns>
        private void TryFree(nint ptr)
        {
            if (_enableBlockFree == 0)
                return;

            for (var blockIndex = 0; blockIndex <= _last; ++blockIndex)
            {
                if (_blocks[blockIndex].Contains(ptr))
                {
                    Union oldUnion;
                    Union readUnion = default;
                    readUnion.Value = Interlocked.Read(ref _blocks[blockIndex].Union.Value);
                    do
                    {
                        oldUnion = readUnion;
                        var newUnion = readUnion;
                        --newUnion.AllocCount;
                        if (newUnion.AllocCount == 0)
                            newUnion.Current = 0;
                        readUnion.Value = Interlocked.CompareExchange(ref _blocks[blockIndex].Union.Value, newUnion.Value, oldUnion.Value);
                    } while (readUnion.Value != oldUnion.Value);

                    return;
                }
            }
        }

        public bool TryRent(uint length, out ArraySegment<byte> memory)
        {
            var block = new Block();
            block.Range.Pointer = default;
            block.Range.Items = (int)length;
            block.BytesPerItem = 1;
            block.Alignment = 128;
            var error = Try(ref block);
            if (error != 0)
            {
                memory = default;
                return false;
            }

            memory = block.Range.Pointer;
            return true;
        }

        public void Return(ArraySegment<byte> array)
        {
            var ptr = array.Array;
            if (ptr == null)
                return;

            TryFree(Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref ptr[array.Offset]));
        }

        /// <summary>
        ///     A range of allocated memory.
        /// </summary>
        /// <remarks>
        ///     The name is perhaps misleading: only in combination with a <see cref="Block" /> does
        ///     a `Range` have sufficient information to represent the number of bytes in an allocation. The reason `Range` is its
        ///     own type that's separate from `Block`
        ///     stems from some efficiency concerns in the implementation details. In most cases, a `Range` is only used in
        ///     conjunction with an associated `Block`.
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        private struct Range
        {
            /// <summary>
            ///     Pointer to the start of this range.
            /// </summary>
            /// <value>Pointer to the start of this range.</value>
            public ArraySegment<byte> Pointer;

            /// <summary>
            ///     Number of items allocated in this range.
            /// </summary>
            /// <remarks>The actual allocation may be larger. See <see cref="Block.AllocatedItems" />.</remarks>
            /// <value>Number of items allocated in this range. </value>
            public int Items;
        }

        /// <summary>
        ///     Represents a memory block allocation request.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct Block
        {
            /// <summary>
            ///     The range of memory encompassed by this block.
            /// </summary>
            /// <value>The range of memory encompassed by this block.</value>
            public Range Range;

            /// <summary>
            ///     Number of bytes per item.
            /// </summary>
            /// <value>Number of bytes per item.</value>
            public int BytesPerItem;

            /// <summary>
            ///     Number of items allocated for.
            /// </summary>
            /// <value>Number of items allocated for.</value>
            public int AllocatedItems;

            /// <summary>
            ///     Log2 of the byte alignment.
            /// </summary>
            /// <remarks>The alignment must always be power of 2. Storing the alignment as its log2 helps enforces this.</remarks>
            /// <value>Log2 of the byte alignment.</value>
            public byte Log2Alignment;

            /// <summary>
            ///     Number of bytes requested for this block.
            /// </summary>
            /// <remarks>The actual allocation size may be larger due to alignment.</remarks>
            /// <value>Number of bytes requested for this block.</value>
            public long Bytes => (long)BytesPerItem * Range.Items;

            /// <summary>
            ///     The alignment.
            /// </summary>
            /// <remarks>
            ///     Must be power of 2 that's greater than or equal to 0.
            ///     Set alignment *before* the allocation is made. Setting it after has no effect on the allocation.
            /// </remarks>
            /// <param name="value">A new alignment. If not a power of 2, it will be rounded up to the next largest power of 2.</param>
            /// <value>The alignment.</value>
            public int Alignment
            {
                get => 1 << Log2Alignment;
                set => Log2Alignment = (byte)(32 - BitOperationsHelpers.LeadingZeroCount((uint)(Math.Max(1, value) - 1)));
            }
        }
    }
}