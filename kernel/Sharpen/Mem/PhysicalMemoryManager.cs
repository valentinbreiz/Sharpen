﻿using Sharpen.Arch;
using Sharpen.Collections;
using Sharpen.Synchronisation;

namespace Sharpen.Mem
{
    public sealed class PhysicalMemoryManager
    {
        public static bool IsInitialized { get; private set; }

        private static BitArray bitmap;
        private static Mutex mutex;

        /// <summary>
        /// Initializes the physical memory manager
        /// </summary>
        public static unsafe void Init()
        {
            // Bit array to store which addresses are free
            bitmap = new BitArray(4096 * 1024 / 4);
            mutex = new Mutex();
            uint aligned = Paging.AlignUp((uint)Heap.CurrentEnd);
            
            Set(0, aligned);
            IsInitialized = true;
        }

        /// <summary>
        /// Gets the first free
        /// </summary>
        /// <returns>The address</returns>
        public static unsafe void* FirstFree()
        {
            mutex.Lock();
            int firstFree = bitmap.FindFirstFree(false);
            mutex.Unlock();
            return (void*)(firstFree * 0x1000);
        }

        /// <summary>
        /// Checks if an address is in use or is free
        /// </summary>
        /// <param name="address">The address</param>
        /// <returns>If the address is free</returns>
        public static unsafe bool IsFree(void* address)
        {
            return !bitmap.IsBitSet((int)((uint)address / 0x1000));
        }

        /// <summary>
        /// Allocates the first free
        /// </summary>
        /// <returns>The address</returns>
        public static unsafe void* Alloc()
        {
            mutex.Lock();
            int firstFree = bitmap.FindFirstFree(true);
            mutex.Unlock();
            void* address = (void*)(firstFree * 0x1000);
            return address;
        }

        /// <summary>
        /// Allocates the first free range
        /// </summary>
        /// <param name="size">The size</param>
        /// <returns>The start address</returns>
        public static unsafe void* AllocRange(int size)
        {
            size = (int)Paging.AlignUp((uint)size) / 0x1000;
            mutex.Lock();
            int firstFree = bitmap.FindFirstFreeRange(size, true);
            mutex.Unlock();
            void* address = (void*)(firstFree * 0x1000);
            return address;
        }

        /// <summary>
        /// Sets the range as used
        /// </summary>
        /// <param name="address">The starting address</param>
        /// <param name="size">The size of the range</param>
        public static void Set(int address, uint size)
        {
            uint start = Paging.AlignUp((uint)address);
            size = Paging.AlignUp(size);
            mutex.Lock();
            for (uint i = start; i < start + size; i += 0x1000)
            {
                bitmap.SetBit((int)(i / 0x1000));
            }
            mutex.Unlock();
        }

        /// <summary>
        /// Sets a single address as used
        /// </summary>
        /// <param name="address">The address</param>
        public static void Set(int address)
        {
            mutex.Lock();
            uint bit = (uint)address / 0x1000;
            bitmap.SetBit((int)bit);
            mutex.Unlock();
        }

        /// <summary>
        /// Frees a block of memory
        /// </summary>
        /// <param name="address">The address</param>
        public static unsafe void Free(void* address)
        {
            uint bit = (uint)address / 0x1000;
            mutex.Lock();
            bitmap.ClearBit((int)bit);
            mutex.Unlock();
        }
    }
}
