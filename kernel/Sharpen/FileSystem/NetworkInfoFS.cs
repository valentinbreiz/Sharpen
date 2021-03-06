﻿using Sharpen.FileSystem.Cookie;
using Sharpen.Lib;
using Sharpen.Mem;
using Sharpen.Net;
using Sharpen.Utilities;

namespace Sharpen.FileSystem
{
    public class NetworkInfoFS
    {
        public enum InfoOPT
        {
            IP,
            SUBNET,
            GATEWAY,
            NS1,
            NS2
        }

        /// <summary>
        /// Initializes the networking info filesystem module
        /// </summary>
        public static unsafe void Init()
        {
            Node node = new Node();
            node.FindDir = findDirImpl;
            node.ReadDir = readDirImpl;
            node.Flags = NodeFlags.DIRECTORY;

            RootPoint dev = new RootPoint("info", node);
            VFS.MountPointNetFS.AddEntry(dev);
        }

        /// <summary>
        /// FS finddir
        /// </summary>
        /// <param name="node">The node</param>
        /// <param name="name">The name to look for</param>
        /// <returns>The node</returns>
        private static unsafe Node findDirImpl(Node node, string name)
        {
            if (name.Equals("ip"))
                return byID(InfoOPT.IP);
            else if (name.Equals("subnet"))
                return byID(InfoOPT.SUBNET);
            else if (name.Equals("gateway"))
                return byID(InfoOPT.GATEWAY);
            else if (name.Equals("ns1"))
                return byID(InfoOPT.NS1);
            else if (name.Equals("ns2"))
                return byID(InfoOPT.NS2);

            return null;
        }

        /// <summary>
        /// Read method for filesystem
        /// </summary>
        /// <param name="node">The node</param>
        /// <param name="offset">The offset</param>
        /// <param name="size">The size</param>
        /// <param name="buffer">The buffer</param>
        /// <returns>The amount of bytes read</returns>
        private static unsafe uint readImpl(Node node, uint offset, uint size, byte[] buffer)
        {
            byte* sourceBuffer = null;

            IDCookie cookie = (IDCookie)node.Cookie;
            InfoOPT opt = (InfoOPT)cookie.ID;

            switch (opt)
            {
                case InfoOPT.IP:
                    sourceBuffer = Network.Settings->IP;
                    break;

                case InfoOPT.SUBNET:
                    sourceBuffer = Network.Settings->Subnet;
                    break;

                case InfoOPT.GATEWAY:
                    sourceBuffer = Network.Settings->Gateway;
                    break;

                case InfoOPT.NS1:
                    sourceBuffer = Network.Settings->DNS1;
                    break;

                case InfoOPT.NS2:
                    sourceBuffer = Network.Settings->DNS2;
                    break;
            }

            int read = Math.Min((int)size, 4);
            Memory.Memcpy(Util.ObjectToVoidPtr(buffer), sourceBuffer, read);

            return (uint)read;
        }

        /// <summary>
        /// Creates a node by its ID
        /// </summary>
        /// <param name="pt">The option</param>
        /// <returns>The node</returns>
        private static unsafe Node byID(InfoOPT opt)
        {
            Node node = new Node();
            node.Read = readImpl;
            node.Size = 4;

            IDCookie cookie = new IDCookie((int)opt);
            node.Cookie = cookie;

            return node;
        }

        /// <summary>
        /// Creates a directory entry by its name
        /// </summary>
        /// <param name="str">The name</param>
        /// <returns>The entry</returns>
        private static unsafe DirEntry* makeByName(string str)
        {
            DirEntry* entry = (DirEntry*)Heap.Alloc(sizeof(DirEntry));
            String.CopyTo(entry->Name, str);
            return entry;
        }

        /// <summary>
        /// FS readdir
        /// </summary>
        /// <param name="node">The node</param>
        /// <param name="index">The index</param>
        /// <returns>The directory entry</returns>
        private static unsafe DirEntry* readDirImpl(Node node, uint index)
        {
            if (index == 0)
                return makeByName("ip");
            else if (index == 1)
                return makeByName("subnet");
            else if (index == 2)
                return makeByName("gateway");
            else if (index == 3)
                return makeByName("ns1");
            else if (index == 4)
                return makeByName("ns2");

            return null;
        }
    }
}
