﻿using Sharpen;
using Sharpen.IO;
using Sharpen.Memory;
using Sharpen.Utilities;

namespace Shell
{
    class Program
    {
        /// <summary>
        /// Try find program in C://exec and run it
        /// </summary>
        /// <param name="name">The program name</param>
        /// <param name="argv">Arguments</param>
        /// <param name="argc">Argument length</param>
        /// <returns>PID</returns>
        private unsafe static int tryRunFromExecDir(string name, string[] argv, int argc)
        {
            string totalString = String.Merge("C://exec/", name);
            int ret = Process.Run(totalString, argv, argc);
            Heap.Free(totalString);
            return ret;
        }

        /// <summary>
        /// Tries to start a process
        /// </summary>
        /// <param name="command">The program name</param>
        /// <param name="name">The program name</param>
        /// <param name="argv">Arguments</param>
        /// <param name="argc">Argument length</param>
        /// <returns>PID</returns>
        private static int tryStartProcess(string command, string[] argv, int argc)
        {
            int ret = Process.Run(command, argv, argc);
            if (ret < 0)
            {
                ret = tryRunFromExecDir(command, argv, argc);

                if (ret < 0)
                {
                    Console.Write(command);
                    Console.WriteLine(": Bad command or filename");
                }
            }

            return ret;
        }

        /// <summary>
        /// Program entry point
        /// </summary>
        /// <param name="args">Arguments</param>
        unsafe static void Main(string[] args)
        {
            Console.WriteLine("Project Sharpen");
            Console.WriteLine("(c) 2016 SharpNative\n");

            string currentDir = Directory.GetCurrentDirectory();
            while (true)
            {
                Console.Write(currentDir);
                Console.Write("> ");

                string read = Console.ReadLine();

                int offsetToSpace = String.IndexOf(read, " ");
                if (offsetToSpace == -1)
                    offsetToSpace = String.Length(read);

                string command = String.SubString(read, 0, offsetToSpace);
                if (command == null)
                {
                    Heap.Free(read);
                    continue;
                }

                string[] argv = null;
                int argc = 1;

                // It has no arguments
                if (read[offsetToSpace] == '\0')
                {
                    argv = new string[2];
                    argv[0] = command;
                    argv[1] = null;
                }
                // It has arguments
                else
                {
                    // Fetch arguments
                    string argumentStart = String.SubString(read, offsetToSpace + 1, String.Length(read) - offsetToSpace - 1);
                    argc = 1 + (String.Count(argumentStart, ' ') + 1);
                    argv = new string[argc + 1];
                    argv[0] = command;

                    // Add arguments
                    int i = 0;
                    int offset = 0;
                    for (; i < argc; i++)
                    {
                        // Find argument end
                        int nextOffset = offset;
                        for (; argumentStart[nextOffset] != ' ' && argumentStart[nextOffset] != '\0'; nextOffset++) ;

                        // Grab argument
                        string arg = String.SubString(argumentStart, offset, nextOffset - offset);
                        offset = nextOffset + 1;
                        argv[i + 1] = arg;
                    }

                    // Add null to end arguments
                    argv[i] = null;
                    Heap.Free(argumentStart);
                }

                if (String.Equals(command, "cd"))
                {
                    if (argc != 2)
                    {
                        Console.WriteLine("Invalid usage of cd: cd [dirname]");
                    }
                    else
                    {
                        if (!Directory.SetCurrentDirectory(argv[1]))
                        {
                            Console.WriteLine("cd: Couldn't change the directory");
                        }
                        else
                        {
                            currentDir = Directory.GetCurrentDirectory();
                            Heap.Free(currentDir);
                        }
                    }
                }
                else if (String.Equals(command, "dir"))
                {
                    Directory dir = Directory.Open(currentDir);

                    uint i = 0;
                    while (true)
                    {
                        Directory.DirEntry entry = dir.Readdir(i);
                        if (entry.Name[0] == '\0')
                            break;

                        string str = Util.CharPtrToString(entry.Name);

                        Console.WriteLine(str);

                        i++;
                    }

                    dir.Close();
                }
                else if (String.Equals(command, "exit"))
                {
                    Process.Exit(0);
                }
                else if (String.Equals(command, "background"))
                {
                    // Try to start a process without waiting until exit
                    string[] offsetArgv = (string[])Array.CreateSubArray(argv, 1, argc - 1);

                    int ret = tryStartProcess(offsetArgv[0], offsetArgv, argc - 1);
                    if (ret > 0)
                    {
                        Console.Write("Process started in background with PID ");
                        Console.Write(ret);
                    }
                    Console.Write('\n');
                    Heap.Free(offsetArgv);
                }
                else
                {
                    // Try to start a process and wait until exit to return to prompt
                    int ret = tryStartProcess(command, argv, argc);
                    Process.WaitForExit(ret);
                }

                // Note: command is in the first entry of argv
                for (int i = 0; i < argc; i++)
                {
                    Heap.Free(argv[i]);
                }
                Heap.Free(read);
                Heap.Free(argv);
            }
        }
    }
}
