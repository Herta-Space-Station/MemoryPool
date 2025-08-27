using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NativeCollections;

namespace Examples
{
    internal sealed unsafe class Program
    {
        private static void Main()
        {
            Test3();
        }

        private static void Test1()
        {
            var array = new byte[1024 * 1024];
            GCHandle.Alloc(array, GCHandleType.Pinned);
            nint mem = Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref MemoryMarshal.GetArrayDataReference(array));
            var tlsf = Tlsf64.tlsf_create_with_pool(mem, (ulong)array.Length);
            var a = Tlsf64.tlsf_malloc(tlsf, 444);
            var b = Tlsf64.tlsf_block_size(a);
            var offset = a - mem;
            Console.WriteLine("Offset: " + offset);
            Console.WriteLine("Size: " + b);
            Console.WriteLine("Test: " + ((nint)(Unsafe.AsPointer(ref array[offset])) - mem));
            Console.WriteLine();

            a = Tlsf64.tlsf_malloc(tlsf, 45646);
            b = Tlsf64.tlsf_block_size(a);
            offset = a - mem;
            Console.WriteLine("Offset: " + offset);
            Console.WriteLine("Size: " + b);
            Console.WriteLine("Test: " + ((nint)(Unsafe.AsPointer(ref array[offset])) - mem));
            Console.WriteLine();

            a = Tlsf64.tlsf_malloc(tlsf, 45646);
            b = Tlsf64.tlsf_block_size(a);
            offset = a - mem;
            Console.WriteLine("Offset: " + offset);
            Console.WriteLine("Size: " + b);
            Console.WriteLine("Test: " + ((nint)(Unsafe.AsPointer(ref array[offset])) - mem));
        }

        private static void Test2()
        {
            var pool = new DynamicMemoryPool(1024 * 1024, 128);
            pool.TryRent(444, 4, out var memory, out var bytes);
            var mem = (nint)Unsafe.AsPointer(ref memory.Array![0]);
            Console.WriteLine("Offset: " + memory.Offset);
            Console.WriteLine("Size: " + bytes);
            Console.WriteLine("Test: " + ((nint)(Unsafe.AsPointer(ref memory.Array![memory.Offset])) - mem));
            pool.Return(memory);
        }

        private static void Test3()
        {
            var pool = new RewindableAllocator(1024 * 1024, true);
            pool.TryRent(444, out var memory);
            var mem = (nint)Unsafe.AsPointer(ref memory.Array![0]);
            Console.WriteLine("Offset: " + memory.Offset);
            Console.WriteLine("Size: " + memory.Count);
            Console.WriteLine("Test: " + ((nint)(Unsafe.AsPointer(ref memory.Array![memory.Offset])) - mem));
            Console.WriteLine();

            var memory1 = memory;

            pool.TryRent(444, out memory);
            mem = (nint)Unsafe.AsPointer(ref memory.Array![0]);
            Console.WriteLine("Offset: " + memory.Offset);
            Console.WriteLine("Size: " + memory.Count);
            Console.WriteLine("Test: " + ((nint)(Unsafe.AsPointer(ref memory.Array![memory.Offset])) - mem));
            Console.WriteLine();

            pool.Return(memory);

            pool.TryRent(444, out memory);
            mem = (nint)Unsafe.AsPointer(ref memory.Array![0]);
            Console.WriteLine("Offset: " + memory.Offset);
            Console.WriteLine("Size: " + memory.Count);
            Console.WriteLine("Test: " + ((nint)(Unsafe.AsPointer(ref memory.Array![memory.Offset])) - mem));
            Console.WriteLine();

            pool.Return(memory);
            pool.Return(memory1);

            pool.TryRent(444, out memory);
            mem = (nint)Unsafe.AsPointer(ref memory.Array![0]);
            Console.WriteLine("Offset: " + memory.Offset);
            Console.WriteLine("Size: " + memory.Count);
            Console.WriteLine("Test: " + ((nint)(Unsafe.AsPointer(ref memory.Array![memory.Offset])) - mem));
        }
    }
}