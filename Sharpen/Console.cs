﻿using Sharpen.Arch;

namespace Sharpen
{
    public unsafe class Console
    {
        private static byte* vidmem = (byte*)0xB8000;
        
        /// <summary>
        /// Cursor X position
        /// </summary>
        public static int X { get; private set; } = 0;

        /// <summary>
        /// Cursor Y position
        /// </summary>
        public static int Y { get; private set; } = 0;

        /// <summary>
        /// Current character attribute
        /// </summary>
        public static byte Attribute { get; set; } = 0x07;

        /// <summary>
        /// Puts a character to the screen at the current location
        /// </summary>
        /// <param name="ch"></param>
        public unsafe static void PutChar(char ch)
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

            MoveCursor();

            // TODO: scroll
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
            for(int i = 0; i < 25 * 80; i++)
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
            int length = String.Length(text);
            for (int i = 0; i < length; i++)
            {
                PutChar(text[i]);
            }
        }

        /// <summary>
        /// Writes a string to the screen with a newline
        /// </summary>
        /// <param name="text">The string</param>
        public static void WriteLine(string text)
        {
            Write(text);
            PutChar('\n');
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
