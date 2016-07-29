﻿using Sharpen.Arch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharpen.Collections
{
    class Fifo
    {
        private byte[] m_buffer;
        private bool m_wait = false;
        private int m_head = 0;
        private int m_tail = 0;
        private int m_size = 0;

        public Fifo(int size)
        {
            m_buffer = new byte[size];
            m_size = size;
        }

        /// <summary>
        /// Read from fifo struct
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public unsafe uint Read(byte* buffer, byte size)
        {
            byte* current = buffer;

            if (m_wait)
            {
                while (m_head == m_tail)
                {
                    CPU.STI();
                    CPU.HLT();
                }
            }

            for (uint i = 0; i < size; i++)
            {
                // Is there data?
                if (m_tail != m_head)
                {

                    *current++ = m_buffer[m_tail++];

                    // Time to flip the tail?
                    if (m_tail >= m_size)
                        m_tail = 0;
                }
                else
                {
                    // We may return if we shouldn't wait
                    return i;
                }
            }

            return size;
        }

        /// <summary>
        /// Write to buffer
        /// </summary>
        /// <param name="buffer">Buffer</param>
        /// <param name="size">Size of buffer</param>
        /// <returns></returns>
        public unsafe uint Write(byte* buffer, uint size)
        {
            byte* current = buffer;

            for(uint i = 0; i < size; i++)
            {
                if (WriteByte(*current++) == 0)
                    return i;
            }

            return size;
        } 

        /// <summary>
        /// Write byte to fifo struct
        /// </summary>
        /// <param name="byt">Byte</param>
        /// <returns></returns>
        public unsafe uint WriteByte(byte byt)
        {
            // Is there any room?
            if((m_head + 1 == m_tail) || ((m_head + 1 == m_size) && m_tail == 0))
            {
                return 0;
            }
            else
            {
                m_buffer[m_head++] = byt;

                // Time to flip the tail?
                if (m_head >= m_size)
                    m_head = 0;
            }

            return 1;
        }
    }
}