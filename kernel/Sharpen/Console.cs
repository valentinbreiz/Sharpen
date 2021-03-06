﻿using Sharpen.Arch;
using Sharpen.Mem;

namespace Sharpen
{
    public unsafe class Console
    {
        private static byte* vidmem = (byte*)0xB8000;

        /// <summary>
        /// Cursor X position
        /// </summary>
        public static int X { get; set; } = 0;

        /// <summary>
        /// Cursor Y position
        /// </summary>
        public static int Y { get; set; } = 0;

        /// <summary>
        /// Current character attribute
        /// </summary>
        public static byte Attribute { get; set; } = 0x07;

        /// <summary>
        /// Puts a character to the screen at the current location
        /// </summary>
        /// <param name="ch"></param>
        public unsafe static void Write(char ch)
        {
            // Enter
            if (ch == '\n')
            {
                X = 0;
                Y++;
            }
            // Carriage return
            else if (ch == '\r')
            {
                X = 0;
            }
            // Backspace
            else if (ch == '\b')
            {
                if (X > 0)
                    X--;
            }
            // Tab
            else if (ch == '\t')
            {
                X = (X + 4) & ~(4 - 1);
            }
            // Normal character
            else
            {
                vidmem[(Y * 80 + X) * 2 + 0] = (byte)ch;
                vidmem[(Y * 80 + X) * 2 + 1] = Attribute;

                X++;
            }

            // New line
            if (X == 80)
            {
                X = 0;
                Y++;
            }

            // End of screen
            if (Y > 24)
            {
                Y = 24;

                // Scrolling
                Memory.Memcpy(vidmem, &vidmem[1 * 80 * 2], 80 * 24 * 2);
                for (int i = 0; i < 80; i++)
                {
                    vidmem[((24 * 80) + i) * 2 + 0] = (byte)' ';
                    vidmem[((24 * 80) + i) * 2 + 1] = Attribute;
                }
            }

            // Move cursor
            MoveCursor();
        }

        /// <summary>
        /// Clears the screen
        /// </summary>
        public static void Clear()
        {
            // Move back to start
            X = 0;
            Y = 0;
            MoveCursor();

            // Clear
            for (int i = 0; i < 25 * 80; i++)
            {
                vidmem[i * 2 + 0] = (byte)' ';
                vidmem[i * 2 + 1] = Attribute;
            }
        }

        /// <summary>
        /// Writes a string to the screen
        /// </summary>
        /// <param name="text">The string</param>
        public static void Write(string text)
        {
            for (int i = 0; text[i] != '\0'; i++)
            {
                Write(text[i]);
            }
        }
        
        /// <summary>
        /// Writes a string to the screen with a newline
        /// </summary>
        /// <param name="text">The string</param>
        public static void WriteLine(string text)
        {
            Write(text);
            Write('\n');
        }
        
        /// <summary>
        /// Writes a hexadecimal integer to the screen
        /// </summary>
        /// <param name="num">The number</param>
        public static void WriteHex(long num)
        {
            if (num == 0)
            {
                Write('0');
                return;
            }

            // Don't print zeroes at beginning of number
            bool noZeroes = true;
            for (int j = 60; j >= 0; j -= 4)
            {
                long tmp = (num >> j) & 0x0F;
                if (tmp == 0 && noZeroes)
                    continue;

                noZeroes = false;
                if (tmp >= 0x0A)
                {
                    Write((char)(tmp - 0x0A + 'A'));
                }
                else
                {
                    Write((char)(tmp + '0'));
                }
            }
        }

        /// <summary>
        /// Writes an integer to the screen
        /// </summary>
        /// <param name="num">The number</param>
        public static void WriteNum(int num)
        {
            if (num == 0)
            {
                Write('0');
                return;
            }

            if (num < 0)
            {
                Write('-');
                num = -num;
            }

            int a = num % 10;
            if (num >= 10)
                WriteNum(num / 10);

            Write((char)('0' + a));
        }

        /// <summary>
        /// Moves the VGA cursor
        /// </summary>
        private static void MoveCursor()
        {
            int index = Y * 80 + X;

            PortIO.Out8(0x3D4, 14);
            PortIO.Out8(0x3D5, (byte)(index >> 8));
            PortIO.Out8(0x3D4, 15);
            PortIO.Out8(0x3D5, (byte)(index & 0xFF));
        }
    }
}
