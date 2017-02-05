﻿using Sharpen.FileSystem.Cookie;
using Sharpen.Mem;
using Sharpen.MultiTasking;
using Sharpen.Utilities;

namespace Sharpen.FileSystem
{
    class ProcFS
    {
        unsafe struct ProcFSInfo
        {
            public fixed char Name[64];
            public fixed char CMDLine[256];
            public int Pid;
            public uint Uptime;
            public int Priority;
            public int ThreadCount;
        }

        /// <summary>
        /// Initializes DevFS
        /// </summary>
        public unsafe static void Init()
        {
            MountPoint mp = new MountPoint();
            mp.Name = "proc";

            Node node = new Node();
            node.FindDir = rootFindDirImpl;
            node.ReadDir = rootReadDirImpl;
            node.Flags = NodeFlags.DIRECTORY;

            mp.Node = node;

            VFS.AddMountPoint(mp);
        }

        /// <summary>
        /// FS finddir
        /// </summary>
        /// <param name="node">The node</param>
        /// <param name="name">The name to look for</param>
        /// <returns>The node</returns>
        private static unsafe Node rootFindDirImpl(Node node, string name)
        {
            int pid = int.Parse(name);
            if (pid < 0)
                return null;

            Task task = Tasking.GetTaskByPID(pid);
            if (task == null)
                return null;

            IDCookie cookie = new IDCookie(task.PID);

            Node taskNode = new Node();
            taskNode.Cookie = (ICookie)cookie;
            taskNode.Flags = NodeFlags.DIRECTORY;
            taskNode.FindDir = procFindDirImpl;
            taskNode.ReadDir = procReadDirImpl;

            return taskNode;
        }

        /// <summary>
        /// FS readdir
        /// </summary>
        /// <param name="node">The node</param>
        /// <param name="index">The index</param>
        /// <returns>The directory entry</returns>
        private static unsafe DirEntry* rootReadDirImpl(Node node, uint index)
        {
            Task current = Tasking.KernelTask;
            int i = 0;
            while (i < index && current.NextTask != Tasking.KernelTask)
            {
                current = current.NextTask;
                i++;
            }

            // Last task entry reached but index is still not reached? Stop.
            if (i < index && current.NextTask == Tasking.KernelTask)
                return null;

            DirEntry* entry = (DirEntry*)Heap.Alloc(sizeof(DirEntry));
            string name = current.PID.ToString();

            i = 0;
            for (; name[i] != '\0'; i++)
                entry->Name[i] = name[i];
            entry->Name[i] = '\0';

            Heap.Free(name);

            return entry;
        }

        /// <summary>
        /// FS finddir
        /// </summary>
        /// <param name="node">The node</param>
        /// <param name="name">The name to look for</param>
        /// <returns>The node</returns>
        private static unsafe Node procFindDirImpl(Node node, string name)
        {
            // File is a command: info
            if (name.Equals("info"))
            {
                Node infoNode = new Node();
                infoNode.Flags = NodeFlags.FILE;
                infoNode.Cookie = node.Cookie; // TODO: clone this?
                infoNode.Size = (uint)sizeof(ProcFSInfo);
                infoNode.Read = infoReadImpl;
                return infoNode;
            }

            // Check if task still exists
            IDCookie cookie = (IDCookie)node.Cookie;
            if (Tasking.GetTaskByPID(cookie.ID) == null)
                return null;

            // File is a thread ID
            int tid = int.Parse(name);
            if (tid < 0)
                return null;

            IDCookie threadCookie = new IDCookie(tid);

            Node threadNode = new Node();
            threadNode.Flags = NodeFlags.FILE;
            threadNode.Cookie = (ICookie)threadCookie;

            return threadNode;
        }

        /// <summary>
        /// FS readdir
        /// </summary>
        /// <param name="node">The node</param>
        /// <param name="index">The index</param>
        /// <returns>The directory entry</returns>
        private static unsafe DirEntry* procReadDirImpl(Node node, uint index)
        {
            // Check if task still exists
            IDCookie cookie = (IDCookie)node.Cookie;
            Task task = Tasking.GetTaskByPID(cookie.ID);
            if (task == null)
                return null;

            DirEntry* entry = (DirEntry*)Heap.Alloc(sizeof(DirEntry));
            string name = null;

            // Index zero is the info file
            if (index == 0)
            {
                name = "info";
            }
            else
            {
                int j = 0;
                Thread current = task.FirstThread;
                while (j < index - 1 && current.NextThread != task.FirstThread)
                {
                    current = current.NextThread;
                    j++;
                }

                // Last thread entry reached but index is still not reached? Stop.
                if (j < index - 1 && current.NextThread == task.FirstThread)
                    return null;

                name = current.TID.ToString();
            }

            int i = 0;
            for (; name[i] != '\0'; i++)
                entry->Name[i] = name[i];
            entry->Name[i] = '\0';

            // A new string is only created here when index != 0
            if (index != 0)
                Heap.Free(name);

            return entry;
        }

        /// <summary>
        /// Read
        /// </summary>
        /// <param name="node">The node</param>
        /// <param name="offset">The offset</param>
        /// <param name="size">The size</param>
        /// <param name="buffer">The buffer</param>
        /// <returns>The amount of bytes written</returns>
        private static unsafe uint infoReadImpl(Node node, uint offset, uint size, byte[] buffer)
        {
            // Check if the task still exists
            IDCookie cookie = (IDCookie)node.Cookie;
            Task task = Tasking.GetTaskByPID(cookie.ID);
            if (task == null)
                return 0;

            ProcFSInfo* info = (ProcFSInfo*)Heap.Alloc(sizeof(ProcFSInfo));
            info->Uptime = task.Uptime;
            info->Priority = (int)task.Priority;
            info->ThreadCount = task.ThreadCount;
            info->Pid = task.PID;

            // Copy name
            int i = 0;
            for (; task.Name[i] != '\0' && i < 63; i++)
                info->Name[i] = task.Name[i];
            info->Name[i] = '\0';

            // Copy cmdline
            i = 0;
            for (; task.CMDLine[i] != '\0' && i < 255; i++)
                info->CMDLine[i] = task.CMDLine[i];
            info->CMDLine[i] = '\0';

            if (size > sizeof(ProcFSInfo))
                size = (uint)sizeof(ProcFSInfo);

            Memory.Memcpy(Util.ObjectToVoidPtr(buffer), info, (int)size);

            Heap.Free(info);

            return size;
        }
    }
}
