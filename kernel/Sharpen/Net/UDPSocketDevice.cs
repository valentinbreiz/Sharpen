﻿using Sharpen.FileSystem;
using Sharpen.FileSystem.Cookie;
using Sharpen.Mem;
using Sharpen.Utilities;

namespace Sharpen.Net
{
    class UDPSocketDevice
    {
        /// <summary>
        /// Opens a UDP client socket
        /// </summary>
        /// <param name="name">The name (IP:PORT)</param>
        /// <returns>The node</returns>
        public static unsafe Node Open(string name)
        {
            int foundIndex = name.IndexOf(':');
            if (foundIndex == -1)
                return null;

            string ip = name.Substring(0, foundIndex);
            string portText = name.Substring(foundIndex + 1, name.Length - foundIndex - 1);

            int port = int.Parse(portText);
            if (port == -1)
            {
                Heap.Free(portText);
                Heap.Free(ip);

                return null;
            }

            UDPSocket sock = new UDPSocket();
            bool found = sock.Connect(ip, (ushort)port);

            if (!found)
            {
                Heap.Free(portText);
                Heap.Free(ip);

                return null;
            }

            Node node = new Node();
            node.Flags = NodeFlags.FILE;
            node.Read = readImpl;
            node.Write = writeImpl;
            node.GetSize = getSizeImpl;
            node.Close = closeImpl;

            UDPSocketCookie cookie = new UDPSocketCookie(sock);
            node.Cookie = cookie;

            Heap.Free(portText);
            Heap.Free(ip);

            return node;
        }

        /// <summary>
        /// Gets the UDP socket from a node
        /// </summary>
        /// <param name="node">The node</param>
        /// <returns>The socket</returns>
        private static unsafe UDPSocket getSocketFromNode(Node node)
        {
            UDPSocketCookie cookie = (UDPSocketCookie)node.Cookie;
            return cookie.Socket;
        }

        /// <summary>
        /// Reads data from the UDP socket
        /// </summary>
        /// <param name="node">The node</param>
        /// <param name="offset">The offset</param>
        /// <param name="size">The size</param>
        /// <param name="buffer">The destination buffer</param>
        /// <returns>The amount of bytes read</returns>
        private static unsafe uint readImpl(Node node, uint offset, uint size, byte[] buffer)
        {
            UDPSocket sock = getSocketFromNode(node);
            if (sock == null)
                return 0;

            return sock.Read((byte*)Util.ObjectToVoidPtr(buffer), size);
        }

        /// <summary>
        /// Writes data to the UDP socket
        /// </summary>
        /// <param name="node">The node</param>
        /// <param name="offset">The offset</param>
        /// <param name="size">The size</param>
        /// <param name="buffer">The destination buffer</param>
        /// <returns>The amount of bytes written</returns>
        private static unsafe uint writeImpl(Node node, uint offset, uint size, byte[] buffer)
        {
            UDPSocket sock = getSocketFromNode(node);
            if (sock == null)
                return 0;

            sock.Send((byte*)Util.ObjectToVoidPtr(buffer), size);

            return size;
        }

        /// <summary>
        /// Gets the size of a UDP socket
        /// </summary>
        /// <param name="node">The node</param>
        /// <returns>The size</returns>
        private static unsafe uint getSizeImpl(Node node)
        {
            UDPSocket sock = getSocketFromNode(node);
            if (sock != null)
                return sock.GetSize();

            return 0;
        }

        /// <summary>
        /// Closes a UDP socket
        /// </summary>
        /// <param name="node">The node</param>
        private static unsafe void closeImpl(Node node)
        {
            node.Cookie.Dispose();
            node.Cookie = null;
        }
    }
}
