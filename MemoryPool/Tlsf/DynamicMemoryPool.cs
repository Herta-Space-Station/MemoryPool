using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CA2208
#pragma warning disable CS8632

// ReSharper disable ALL

namespace NativeCollections
{
    /// <summary>
    ///     Native dynamic (Two-Level Segregated Fit) memory pool
    ///     https://github.com/mattconte/tlsf
    /// </summary>
    public sealed class DynamicMemoryPool : IDisposable
    {
        /// <summary>
        ///     Buffer
        /// </summary>
        private readonly nint _buffer;

        /// <summary>
        ///     Handle
        /// </summary>
        private readonly nint _handle;

        /// <summary>
        ///     Array
        /// </summary>
        private readonly byte[] _array;

        /// <summary>
        ///     Gc handle
        /// </summary>
        private readonly GCHandle _gcHandle;

        /// <summary>
        ///     Size
        /// </summary>
        private readonly nuint _size;

        /// <summary>
        ///     Blocks
        /// </summary>
        private readonly nuint _blocks;

        /// <summary>
        ///     Structure
        /// </summary>
        /// <param name="size">Size</param>
        /// <param name="blocks">Blocks</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DynamicMemoryPool(nuint size, nuint blocks)
        {
            nint handle;
            if (Unsafe.SizeOf<nint>() == 8)
            {
                var bytes = Tlsf64.align_up(Tlsf64.tlsf_size() + Tlsf64.tlsf_pool_overhead() + blocks * Tlsf64.tlsf_alloc_overhead() + size, 8);
                bytes = Tlsf64.align_up((ulong)bytes, (ulong)Unsafe.SizeOf<nint>()) + (ulong)Unsafe.SizeOf<nint>() - 1;
                _array = new byte[bytes];
                _gcHandle = GCHandle.Alloc(_array, GCHandleType.Pinned);
                var buffer = Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref ArrayHelpers.GetArrayDataReference(_array));
                _buffer = buffer;
                buffer = (nint)Tlsf64.align_up((ulong)buffer, (ulong)Unsafe.SizeOf<nint>());
                handle = Tlsf64.tlsf_create_with_pool(buffer, bytes);
            }
            else
            {
                var bytes = Tlsf32.align_up((uint)(Tlsf32.tlsf_size() + Tlsf32.tlsf_pool_overhead() + blocks * Tlsf32.tlsf_alloc_overhead() + size), 4);
                bytes = (uint)(Tlsf32.align_up((uint)bytes, (uint)Unsafe.SizeOf<nint>()) + Unsafe.SizeOf<nint>() - 1);
                _array = new byte[bytes];
                _gcHandle = GCHandle.Alloc(_array, GCHandleType.Pinned);
                var buffer = Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref ArrayHelpers.GetArrayDataReference(_array));
                _buffer = buffer;
                buffer = (nint)Tlsf32.align_up((uint)buffer, (uint)Unsafe.SizeOf<nint>());
                handle = Tlsf32.tlsf_create_with_pool(buffer, bytes);
            }

            _handle = handle;
            _size = size;
            _blocks = blocks;
        }

        /// <summary>
        ///     Is created
        /// </summary>
        public bool IsCreated => _gcHandle.IsAllocated;

        /// <summary>
        ///     Size
        /// </summary>
        public nuint Size => _size;

        /// <summary>
        ///     Blocks
        /// </summary>
        public nuint Blocks => _blocks;

        /// <summary>
        ///     Dispose
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (!_gcHandle.IsAllocated)
                return;
            _gcHandle.Free();
        }

        /// <summary>
        ///     Reset
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            var size = _size;
            var blocks = _blocks;
            nuint bytes;
            var buffer = _handle;
            if (Unsafe.SizeOf<nint>() == 8)
            {
                bytes = (nuint)Tlsf64.align_up(Tlsf64.tlsf_size() + Tlsf64.tlsf_pool_overhead() + blocks * Tlsf64.tlsf_alloc_overhead() + size, 8);
                Tlsf64.tlsf_create_with_pool(buffer, bytes);
            }
            else
            {
                bytes = Tlsf32.align_up((uint)(Tlsf32.tlsf_size() + Tlsf32.tlsf_pool_overhead() + blocks * Tlsf32.tlsf_alloc_overhead() + size), 4);
                Tlsf32.tlsf_create_with_pool(buffer, (uint)bytes);
            }
        }

        /// <summary>
        ///     Try rent
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRent(nuint size, nuint alignment, out ArraySegment<byte> memory, out nuint bytes)
        {
            if (Unsafe.SizeOf<nint>() == 8)
            {
                var ptr = Tlsf64.tlsf_memalign(_handle, alignment, size);
                if (ptr != 0)
                {
                    bytes = (nuint)Tlsf64.tlsf_block_size(ptr);
                    memory = new ArraySegment<byte>(_array, (int)(ptr - _buffer), (int)size);
                    return true;
                }
                else
                {
                    memory = default;
                    bytes = 0;
                    return false;
                }
            }
            else
            {
                var ptr = Tlsf32.tlsf_memalign(_handle, (uint)alignment, (uint)size);
                if (ptr != 0)
                {
                    bytes = Tlsf32.tlsf_block_size(ptr);
                    memory = new ArraySegment<byte>(_array, (int)(ptr - _buffer), (int)size);
                    return true;
                }
                else
                {
                    memory = default;
                    bytes = 0;
                    return false;
                }
            }
        }

        /// <summary>
        ///     Return buffer
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(ArraySegment<byte> memory)
        {
            var ptr = _handle + memory.Offset;
            if (Unsafe.SizeOf<nint>() == 8)
                Tlsf64.tlsf_free(_handle, ptr);
            else
                Tlsf32.tlsf_free(_handle, ptr);
        }
    }
}