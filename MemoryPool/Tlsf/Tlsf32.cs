using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable ALL

#pragma warning disable CS1591

namespace NativeCollections
{
    public static class Tlsf32
    {
        public const int SL_INDEX_COUNT_LOG2 = 5;
        public const int ALIGN_SIZE_LOG2 = 2;
        public const int ALIGN_SIZE = 1 << ALIGN_SIZE_LOG2;
        public const int FL_INDEX_MAX = 30;
        public const int SL_INDEX_COUNT = 1 << SL_INDEX_COUNT_LOG2;
        public const int FL_INDEX_SHIFT = SL_INDEX_COUNT_LOG2 + ALIGN_SIZE_LOG2;
        public const int FL_INDEX_COUNT = FL_INDEX_MAX - FL_INDEX_SHIFT + 1;
        public const int SMALL_BLOCK_SIZE = 1 << FL_INDEX_SHIFT;
        public const uint block_header_free_bit = 1 << 0;
        public const uint block_header_prev_free_bit = 1 << 1;
        public const uint block_header_overhead = sizeof(uint);
        public const uint block_start_offset = sizeof(uint) + sizeof(uint);
        public const uint block_size_min = 16 - sizeof(uint);
        public const uint block_size_max = (uint)1 << FL_INDEX_MAX;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void memcpy(ref byte dst, ref byte src, uint size) => Unsafe.CopyBlockUnaligned(ref dst, ref src, (uint)size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int tlsf_fls(uint word)
        {
            var bit = 32 - BitOperationsHelpers.LeadingZeroCount(word);
            return bit - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int tlsf_ffs(uint word)
        {
            var reverse = word & (~word + 1);
            var bit = 32 - BitOperationsHelpers.LeadingZeroCount(reverse);
            return bit - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int tlsf_fls_sizet(uint size) => tlsf_fls(size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint tlsf_min(uint a, uint b) => a < b ? a : b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint tlsf_max(uint a, uint b) => a > b ? a : b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint block_size(NativePointer<block_header_t> block) => block.AsRef().size & ~(block_header_free_bit | block_header_prev_free_bit);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void block_set_size(NativePointer<block_header_t> block, uint size)
        {
            var oldsize = block.AsRef().size;
            block.AsRef().size = size | (oldsize & (block_header_free_bit | block_header_prev_free_bit));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int block_is_last(NativePointer<block_header_t> block) => block_size(block) == 0 ? 1 : 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int block_is_free(NativePointer<block_header_t> block) => (int)(block.AsRef().size & block_header_free_bit);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void block_set_free(NativePointer<block_header_t> block) => block.AsRef().size |= block_header_free_bit;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void block_set_used(NativePointer<block_header_t> block) => block.AsRef().size &= ~block_header_free_bit;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int block_is_prev_free(NativePointer<block_header_t> block) => (int)(block.AsRef().size & block_header_prev_free_bit);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void block_set_prev_free(NativePointer<block_header_t> block) => block.AsRef().size |= block_header_prev_free_bit;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void block_set_prev_used(NativePointer<block_header_t> block) => block.AsRef().size &= ~block_header_prev_free_bit;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativePointer<block_header_t> block_from_ptr(nint ptr) => ptr - (nint)block_start_offset;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint block_to_ptr(NativePointer<block_header_t> block) => block + (nint)(int)block_start_offset;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativePointer<block_header_t> offset_to_block(nint ptr, uint size) => ptr + (nint)size;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativePointer<block_header_t> block_prev(NativePointer<block_header_t> block) => block.AsRef().prev_phys_block;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativePointer<block_header_t> block_next(NativePointer<block_header_t> block)
        {
            var next = offset_to_block(block_to_ptr(block), block_size(block) - block_header_overhead);
            return next;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativePointer<block_header_t> block_link_next(NativePointer<block_header_t> block)
        {
            var next = block_next(block);
            next.AsRef().prev_phys_block = block;
            return next;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void block_mark_as_free(NativePointer<block_header_t> block)
        {
            var next = block_link_next(block);
            block_set_prev_free(next);
            block_set_free(block);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void block_mark_as_used(NativePointer<block_header_t> block)
        {
            var next = block_next(block);
            block_set_prev_used(next);
            block_set_used(block);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint align_up(uint x, uint align) => (x + (align - 1)) & ~(align - 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint align_down(uint x, uint align) => x - (x & (align - 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint align_ptr(nint ptr, uint align)
        {
            var aligned = (ptr + (nint)(align - 1)) & ~ (nint)(align - 1);
            return aligned;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint adjust_request_size(uint size, uint align)
        {
            uint adjust = 0;
            if (size != 0)
            {
                var aligned = align_up(size, align);
                if (aligned < block_size_max)
                    adjust = tlsf_max(aligned, block_size_min);
            }

            return adjust;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void mapping_insert(uint size, NativePointer<int> fli, NativePointer<int> sli)
        {
            int fl, sl;
            if (size < SMALL_BLOCK_SIZE)
            {
                fl = 0;
                sl = (int)size / (SMALL_BLOCK_SIZE / SL_INDEX_COUNT);
            }
            else
            {
                fl = tlsf_fls_sizet(size);
                sl = (int)(size >> (fl - SL_INDEX_COUNT_LOG2)) ^ (1 << SL_INDEX_COUNT_LOG2);
                fl -= FL_INDEX_SHIFT - 1;
            }

            fli.AsRef() = fl;
            sli.AsRef() = sl;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void mapping_search(uint size, NativePointer<int> fli, NativePointer<int> sli)
        {
            if (size >= SMALL_BLOCK_SIZE)
            {
                var round = (uint)((1 << (tlsf_fls_sizet(size) - SL_INDEX_COUNT_LOG2)) - 1);
                size += round;
            }

            mapping_insert(size, fli, sli);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativePointer<block_header_t> search_suitable_block(NativePointer<control_t> control, NativePointer<int> fli, NativePointer<int> sli)
        {
            var fl = fli.AsRef();
            var sl = sli.AsRef();
            var sl_map = Unsafe.Add(ref Unsafe.As<sl_bitmap_t, uint>(ref control.AsRef().sl_bitmap), (nint)fl) & (~0U << sl);
            if (!(sl_map != 0))
            {
                var fl_map = control.AsRef().fl_bitmap & (~0U << (fl + 1));
                if (!(fl_map != 0))
                    return 0;
                fl = tlsf_ffs(fl_map);
                fli.AsRef() = fl;
                sl_map = Unsafe.Add(ref Unsafe.As<sl_bitmap_t, uint>(ref control.AsRef().sl_bitmap), (nint)fl);
            }

            sl = tlsf_ffs(sl_map);
            sli.AsRef() = sl;
            return Unsafe.Add(ref Unsafe.As<uint, NativePointer<block_header_t>>(ref control.AsRef().blocks.get(fl)), (nint)sl);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void remove_free_block(NativePointer<control_t> control, NativePointer<block_header_t> block, int fl, int sl)
        {
            var prev = block.AsRef().prev_free;
            var next = block.AsRef().next_free;
            next.AsRef().prev_free = prev;
            prev.AsRef().next_free = next;
            if (Unsafe.Add(ref Unsafe.As<uint, NativePointer<block_header_t>>(ref control.AsRef().blocks.get(fl)), (nint)sl) == block)
            {
                Unsafe.Add(ref Unsafe.As<uint, NativePointer<block_header_t>>(ref control.AsRef().blocks.get(fl)), (nint)sl) = next;
                if (next == NativePointer<block_header_t>.Create(ref control.AsRef().block_null))
                {
                    Unsafe.Add(ref Unsafe.As<sl_bitmap_t, uint>(ref control.AsRef().sl_bitmap), (nint)fl) &= ~(1U << sl);
                    if (!(Unsafe.Add(ref Unsafe.As<sl_bitmap_t, uint>(ref control.AsRef().sl_bitmap), (nint)fl) != 0))
                        control.AsRef().fl_bitmap &= ~(1U << fl);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void insert_free_block(NativePointer<control_t> control, NativePointer<block_header_t> block, int fl, int sl)
        {
            var current = Unsafe.Add(ref Unsafe.As<uint, NativePointer<block_header_t>>(ref control.AsRef().blocks.get(fl)), (nint)sl);
            block.AsRef().next_free = current;
            block.AsRef().prev_free = NativePointer<block_header_t>.Create(ref control.AsRef().block_null);
            current.AsRef().prev_free = block;
            Unsafe.Add(ref Unsafe.As<uint, NativePointer<block_header_t>>(ref control.AsRef().blocks.get(fl)), (nint)sl) = block;
            control.AsRef().fl_bitmap |= 1U << fl;
            Unsafe.Add(ref Unsafe.As<sl_bitmap_t, uint>(ref control.AsRef().sl_bitmap), (nint)fl) |= 1U << sl;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void block_remove(NativePointer<control_t> control, NativePointer<block_header_t> block)
        {
            int fl = 0, sl = 0;
            mapping_insert(block_size(block), NativePointer<int>.Create(ref fl), NativePointer<int>.Create(ref sl));
            remove_free_block(control, block, fl, sl);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void block_insert(NativePointer<control_t> control, NativePointer<block_header_t> block)
        {
            int fl = 0, sl = 0;
            mapping_insert(block_size(block), NativePointer<int>.Create(ref fl), NativePointer<int>.Create(ref sl));
            insert_free_block(control, block, fl, sl);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int block_can_split(NativePointer<block_header_t> block, uint size) => block_size(block) >= (uint)Unsafe.SizeOf<block_header_t>() + size ? 1 : 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativePointer<block_header_t> block_split(NativePointer<block_header_t> block, uint size)
        {
            var remaining = offset_to_block(block_to_ptr(block), size - block_header_overhead);
            var remain_size = block_size(block) - (size + block_header_overhead);
            block_set_size(remaining, remain_size);
            block_set_size(block, size);
            block_mark_as_free(remaining);
            return remaining;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativePointer<block_header_t> block_absorb(NativePointer<block_header_t> prev, NativePointer<block_header_t> block)
        {
            prev.AsRef().size += block_size(block) + block_header_overhead;
            block_link_next(prev);
            return prev;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativePointer<block_header_t> block_merge_prev(NativePointer<control_t> control, NativePointer<block_header_t> block)
        {
            if (block_is_prev_free(block) != 0)
            {
                var prev = block_prev(block);
                block_remove(control, prev);
                block = block_absorb(prev, block);
            }

            return block;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativePointer<block_header_t> block_merge_next(NativePointer<control_t> control, NativePointer<block_header_t> block)
        {
            var next = block_next(block);
            if (block_is_free(next) != 0)
            {
                block_remove(control, next);
                block = block_absorb(block, next);
            }

            return block;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void block_trim_free(NativePointer<control_t> control, NativePointer<block_header_t> block, uint size)
        {
            if (block_can_split(block, size) != 0)
            {
                var remaining_block = block_split(block, size);
                block_link_next(block);
                block_set_prev_free(remaining_block);
                block_insert(control, remaining_block);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void block_trim_used(NativePointer<control_t> control, NativePointer<block_header_t> block, uint size)
        {
            if (block_can_split(block, size) != 0)
            {
                var remaining_block = block_split(block, size);
                block_set_prev_used(remaining_block);
                remaining_block = block_merge_next(control, remaining_block);
                block_insert(control, remaining_block);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativePointer<block_header_t> block_trim_free_leading(NativePointer<control_t> control, NativePointer<block_header_t> block, uint size)
        {
            var remaining_block = block;
            if (block_can_split(block, size) != 0)
            {
                remaining_block = block_split(block, size - block_header_overhead);
                block_set_prev_free(remaining_block);
                block_link_next(block);
                block_insert(control, block);
            }

            return remaining_block;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativePointer<block_header_t> block_locate_free(NativePointer<control_t> control, uint size)
        {
            int fl = 0, sl = 0;
            NativePointer<block_header_t> block = 0;
            if (size != 0)
            {
                mapping_search(size, NativePointer<int>.Create(ref fl), NativePointer<int>.Create(ref sl));
                if (fl < FL_INDEX_COUNT)
                    block = search_suitable_block(control, NativePointer<int>.Create(ref fl), NativePointer<int>.Create(ref sl));
            }

            if (block != 0)
                remove_free_block(control, block, fl, sl);
            return block;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint block_prepare_used(NativePointer<control_t> control, NativePointer<block_header_t> block, uint size)
        {
            nint p = 0;
            if (block != 0)
            {
                block_trim_free(control, block, size);
                block_mark_as_used(block);
                p = block_to_ptr(block);
            }

            return p;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void control_construct(NativePointer<control_t> control)
        {
            int i, j;
            control.AsRef().block_null.next_free = NativePointer<block_header_t>.Create(ref control.AsRef().block_null);
            control.AsRef().block_null.prev_free = NativePointer<block_header_t>.Create(ref control.AsRef().block_null);
            control.AsRef().fl_bitmap = 0;
            for (i = 0; i < FL_INDEX_COUNT; ++i)
            {
                Unsafe.Add(ref Unsafe.As<sl_bitmap_t, uint>(ref control.AsRef().sl_bitmap), (nint)i) = 0;
                for (j = 0; j < SL_INDEX_COUNT; ++j)
                    Unsafe.Add(ref Unsafe.As<uint, NativePointer<block_header_t>>(ref control.AsRef().blocks.get(i)), (nint)j) = NativePointer<block_header_t>.Create(ref control.AsRef().block_null);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint tlsf_block_size(nint ptr)
        {
            uint size = 0;
            if (ptr != 0)
            {
                var block = block_from_ptr(ptr);
                size = block_size(block);
            }

            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint tlsf_size() => (uint)Unsafe.SizeOf<control_t>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint tlsf_align_size() => ALIGN_SIZE;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint tlsf_block_size_min() => block_size_min;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint tlsf_block_size_max() => block_size_max;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint tlsf_pool_overhead() => 2 * block_header_overhead;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint tlsf_alloc_overhead() => block_header_overhead;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint tlsf_add_pool(nint tlsf, nint mem, uint bytes)
        {
            NativePointer<block_header_t> block;
            NativePointer<block_header_t> next;
            var pool_overhead = tlsf_pool_overhead();
            var pool_bytes = align_down(bytes - pool_overhead, ALIGN_SIZE);
            if ((long)mem % ALIGN_SIZE != 0)
                return 0;
            if (pool_bytes < block_size_min || pool_bytes > block_size_max)
                return 0;
            const uint size = 4294967292U;
            block = offset_to_block(mem, size);
            block_set_size(block, pool_bytes);
            block_set_free(block);
            block_set_prev_used(block);
            block_insert(tlsf, block);
            next = block_link_next(block);
            block_set_size(next, 0);
            block_set_used(next);
            block_set_prev_free(next);
            return mem;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void tlsf_remove_pool(nint tlsf, nint pool)
        {
            var control = (NativePointer<control_t>)tlsf;
            var block = offset_to_block(pool, unchecked((uint)-(int)block_header_overhead));
            int fl = 0, sl = 0;
            mapping_insert(block_size(block), NativePointer<int>.Create(ref fl), NativePointer<int>.Create(ref sl));
            remove_free_block(control, block, fl, sl);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint tlsf_create(nint mem)
        {
            if (mem % ALIGN_SIZE != 0)
                return 0;
            control_construct(mem);
            return mem;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint tlsf_create_with_pool(nint mem, uint bytes)
        {
            var tlsf = tlsf_create(mem);
            tlsf_add_pool(tlsf, mem + (nint)tlsf_size(), bytes - tlsf_size());
            return tlsf;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint tlsf_get_pool(nint tlsf) => tlsf + (nint)tlsf_size();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint tlsf_malloc(nint tlsf, uint size)
        {
            var control = (NativePointer<control_t>)tlsf;
            var adjust = adjust_request_size(size, ALIGN_SIZE);
            var block = block_locate_free(control, adjust);
            return block_prepare_used(control, block, adjust);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint tlsf_memalign(nint tlsf, uint align, uint size)
        {
            var control = (NativePointer<control_t>)tlsf;
            var adjust = adjust_request_size(size, ALIGN_SIZE);
            var gap_minimum = (uint)Unsafe.SizeOf<block_header_t>();
            var size_with_gap = adjust_request_size(adjust + align + gap_minimum, align);
            var aligned_size = adjust != 0 && align > ALIGN_SIZE ? size_with_gap : adjust;
            var block = block_locate_free(control, aligned_size);
            if (block != 0)
            {
                var ptr = block_to_ptr(block);
                var aligned = align_ptr(ptr, align);
                var gap = (uint)(aligned - ptr);
                if (gap != 0 && gap < gap_minimum)
                {
                    var gap_remain = gap_minimum - gap;
                    var offset = tlsf_max(gap_remain, align);
                    var next_aligned = aligned + (nint)offset;
                    aligned = align_ptr(next_aligned, align);
                    gap = (uint)(aligned - ptr);
                }

                if (gap != 0)
                    block = block_trim_free_leading(control, block, gap);
            }

            return block_prepare_used(control, block, adjust);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void tlsf_free(nint tlsf, nint ptr)
        {
            if (ptr != 0)
            {
                var control = (NativePointer<control_t>)tlsf;
                var block = block_from_ptr(ptr);
                block_mark_as_free(block);
                block = block_merge_prev(control, block);
                block = block_merge_next(control, block);
                block_insert(control, block);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint tlsf_realloc(nint tlsf, nint ptr, uint size)
        {
            var control = (NativePointer<control_t>)tlsf;
            nint p = 0;
            if (ptr != 0 && size == 0)
                tlsf_free(tlsf, ptr);
            else if (!(ptr != 0))
                p = tlsf_malloc(tlsf, size);
            else
            {
                var block = block_from_ptr(ptr);
                var next = block_next(block);
                var cursize = block_size(block);
                var combined = cursize + block_size(next) + block_header_overhead;
                var adjust = adjust_request_size(size, ALIGN_SIZE);
                if (adjust > cursize && (!(block_is_free(next) != 0) || adjust > combined))
                {
                    p = tlsf_malloc(tlsf, size);
                    if (p != 0)
                    {
                        var minsize = tlsf_min(cursize, size);
                        memcpy(ref new NativePointer<byte>(p).AsRef(), ref new NativePointer<byte>(ptr).AsRef(), minsize);
                        tlsf_free(tlsf, ptr);
                    }
                }
                else
                {
                    if (adjust > cursize)
                    {
                        block_merge_next(control, block);
                        block_mark_as_used(block);
                    }

                    block_trim_used(control, block, adjust);
                    p = ptr;
                }
            }

            return p;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct block_header_t
        {
            public NativePointer<block_header_t> prev_phys_block;
            public uint size;
            public NativePointer<block_header_t> next_free;
            public NativePointer<block_header_t> prev_free;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct control_t
        {
            public block_header_t block_null;
            public uint fl_bitmap;
            public sl_bitmap_t sl_bitmap;
            public blocks_t blocks;
        }

        public static ref uint get(ref this sl_bitmap_t src, int index) => ref Unsafe.Add(ref Unsafe.As<sl_bitmap_t, uint>(ref src), index);

        [StructLayout(LayoutKind.Sequential)]
        public struct sl_bitmap_t
        {
            public sl_bitmap_t_16 element0;
            public sl_bitmap_t_8 element1;

            public struct sl_bitmap_t_16
            {
                public sl_bitmap_t_8 element0;
                public sl_bitmap_t_8 element1;
            }

            public struct sl_bitmap_t_8
            {
                public sl_bitmap_t_4 element0;
                public sl_bitmap_t_4 element1;
            }

            public struct sl_bitmap_t_4
            {
                public sl_bitmap_t_2 element0;
                public sl_bitmap_t_2 element1;
            }

            public struct sl_bitmap_t_2
            {
                public uint element0;
                public uint element1;
            }
        }

        public static ref uint get(ref this blocks_t src, int index) => ref Unsafe.Add(ref Unsafe.As<blocks_t, uint>(ref src), index);

        [StructLayout(LayoutKind.Sequential)]
        public struct blocks_t
        {
            public blocks_t_512 element0;
            public blocks_t_256 element1;

            public struct blocks_t_512
            {
                public blocks_t_256 element0;
                public blocks_t_256 element1;
            }

            public struct blocks_t_256
            {
                public blocks_t_128 element0;
                public blocks_t_128 element1;
            }

            public struct blocks_t_128
            {
                public blocks_t_64 element0;
                public blocks_t_64 element1;
            }

            public struct blocks_t_64
            {
                public blocks_t_32 element0;
                public blocks_t_32 element1;
            }

            public struct blocks_t_32
            {
                public blocks_t_16 element0;
                public blocks_t_16 element1;
            }

            public struct blocks_t_16
            {
                public blocks_t_8 element0;
                public blocks_t_8 element1;
            }

            public struct blocks_t_8
            {
                public blocks_t_4 element0;
                public blocks_t_4 element1;
            }

            public struct blocks_t_4
            {
                public blocks_t_2 element0;
                public blocks_t_2 element1;
            }

            public struct blocks_t_2
            {
                public uint element0;
                public uint element1;
            }
        }
    }
}