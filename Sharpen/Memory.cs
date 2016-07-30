﻿using System;

namespace Sharpen
{
    public sealed class Memory
    {
        /// <summary>
        /// Copies the value of num bytes from the source to the destination
        /// </summary>
        /// <param name="destination">The destination</param>
        /// <param name="source">The source</param>
        /// <param name="num">The number of bytes</param>
        public static extern unsafe void Memcpy(void* destination, void* source, int num);

        internal static unsafe bool Compare(char* s1, char *s2, int n)
        {
            for(int i = 0; i < n; i++)
            {
                if (s1[i] != s2[i])
                    return false;
            }


            return true;
        }

        /// <summary>
        /// Sets the num bytes of the block of memory pointed by ptr to the specified value
        /// </summary>
        /// <param name="ptr">The destination pointer</param>
        /// <param name="value">The value</param>
        /// <param name="num">The number of bytes</param>
        public static extern unsafe void Memset(void* ptr, int value, int num);
    }
}
