﻿// #define UDP_DEBUG
// #define UDP_DEBUG_PACKETS

using Sharpen.Mem;
using Sharpen.Utilities;
using Sharpen.Lib;
using System.Runtime.InteropServices;

namespace Sharpen.Net
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct UDPHeader
    {
        public ushort SourcePort;
        public ushort DestinationPort;
        public ushort Length;
        public ushort Checksum;
    }

    /// <summary>
    /// LAYER 2 - UDP
    /// </summary>
    class UDP
    {
        private const byte PROTOCOL_UDP = 0x11;

        // TODO: find better way to handle this, this is wasted space
        private static UDPPacketHandler[] m_handlers;
        private static UDPSocket[] m_sockets;

        private static ushort m_portOffset = 49100;

        /// <summary>
        /// UDP packet handler
        /// </summary>
        /// <param name="xid"></param>
        /// <param name="buffer"></param>
        /// <param name="size"></param>
        public unsafe delegate void UDPPacketHandler(byte[] ip, ushort sourcePort, ushort destPort, byte* buffer, uint size);

        /// <summary>
        /// Initializes UDP
        /// </summary>
        public static unsafe void Init()
        {
            m_handlers = new UDPPacketHandler[65536];
            m_sockets = new UDPSocket[65536];

            IPV4.RegisterHandler(PROTOCOL_UDP, handler);

#if UDP_DEBUG
            Console.WriteLine("[UDP] Registered handler on 0x11");
#endif
        }

        /// <summary>
        /// Bind UDP port
        /// </summary>
        /// <param name="port">The port</param>
        /// <param name="handler">The callback</param>
        public static void Bind(ushort port, UDPPacketHandler handler)
        {
#if UDP_DEBUG
            Console.Write("[UDP] Bind Port: ");
            Console.WriteNum(port);
            Console.WriteLine("");
#endif

            m_handlers[port] = handler;
        }

        /// <summary>
        /// Binds a UDP socket on a free port
        /// </summary>
        /// <param name="socket">The socket</param>
        public static unsafe void BindSocketRequest(UDPSocket socket)
        {
            ushort port = RequestPort();

            socket.SourcePort = port;

            BindSocket(socket);
        }

        /// <summary>
        /// Binds a UDP socket
        /// </summary>
        /// <param name="socket">The socket</param>
        public static unsafe void BindSocket(UDPSocket socket)
        {
            ushort port = socket.SourcePort;

            m_handlers[port] = socketHandler;
            m_sockets[port] = socket;
        }

        /// <summary>
        /// Unbinds a UDP socket
        /// </summary>
        /// <param name="socket">The socket</param>
        public static unsafe void UnBindSocket(UDPSocket socket)
        {
            m_handlers[socket.SourcePort] = null;
            m_sockets[socket.SourcePort] = null;

            socket.SourcePort = 0;
        }

        /// <summary>
        /// Handles UDP socket incoming data
        /// </summary>
        /// <param name="ip">The source IP</param>
        /// <param name="sourcePort">The source port</param>
        /// <param name="destPort">The destination port</param>
        /// <param name="buffer">The buffer</param>
        /// <param name="size">The size to read</param>
        public static unsafe void socketHandler(byte[] ip, ushort sourcePort, ushort destPort, byte* buffer, uint size)
        {
            UDPSocket sock = m_sockets[destPort];

            if (sock != null)
                sock.Receive(ip, buffer, size);
        }

        /// <summary>
        /// Unbind UDP port
        /// </summary>
        /// <param name="port">The port</param>
        /// <param name="handler">The callback</param>
        public static void UnBind(ushort port)
        {
#if UDP_DEBUG
            Console.Write("[UDP] Unbind Port: ");
            Console.WriteNum(port);
            Console.WriteLine("");
#endif

            m_handlers[port] = null;
        }

        /// <summary>
        /// Get random port :)
        /// </summary>
        /// <returns>A random free port</returns>
        public static ushort RequestPort()
        {
            return m_portOffset++;
        }

        /// <summary>
        /// UDP packet handler
        /// </summary>
        /// <param name="xid">Identification ID</param>
        /// <param name="buffer">Buffer pointer</param>
        /// <param name="size">Packet size</param>
        private static unsafe void handler(byte[] sourceIp, byte* buffer, uint size)
        {
            UDPHeader* header = (UDPHeader*)buffer;

            ushort destPort = Byte.ReverseBytes(header->DestinationPort);
            ushort sourcePort = Byte.ReverseBytes(header->SourcePort);

#if UDP_DEBUG_PACKETS
            Console.Write("[UDP] Receive from ");
            Console.WriteNum(sourcePort);
            Console.Write(" to ");
            Console.WriteNum(destPort);
            Console.WriteLine("");
#endif

            m_handlers[destPort]?.Invoke(sourceIp, sourcePort, destPort, buffer + sizeof(UDPHeader), (uint)(Byte.ReverseBytes(header->Length) - sizeof(UDPHeader)));
        }

        /// <summary>
        /// Add UDP header to packet
        /// </summary>
        /// <param name="packet">Packet structure</param>
        /// <param name="destIP">Destination IP</param>
        /// <param name="sourcePort">Source port</param>
        /// <param name="DestinationPort">Destination port</param>
        /// <returns>The pointer to the header</returns>
        private static unsafe UDPHeader* addHeader(NetPacketDesc* packet, byte[] destIP, ushort sourcePort, ushort DestinationPort)
        {
            packet->start -= (short)sizeof(UDPHeader);

            UDPHeader* header = (UDPHeader*)(packet->buffer + packet->start);

            header->SourcePort = Byte.ReverseBytes(sourcePort);
            header->DestinationPort = Byte.ReverseBytes(DestinationPort);
            header->Length = Byte.ReverseBytes((ushort)(packet->end - packet->start));

            header->Checksum = 0;

            return header;
        }

        /// <summary>
        /// Send UDP data
        /// </summary>
        /// <param name="destMac">Destination MAC</param>
        /// <param name="destIP">Destination IP</param>
        /// <param name="srcPort">Source port</param>
        /// <param name="DestPort">Destination port</param>
        /// <param name="data">Data pointer</param>
        /// <param name="size">Data size</param>
        public static unsafe void Send(byte[] destMac, byte[] destIP, ushort srcPort, ushort DestPort, byte[] data, int size)
        {
            /**
             * No support for packets over 1500 bytes
             */
            if (size >= 1500)
                return;

            NetPacketDesc* packet = NetPacket.Alloc();

            Memory.Memcpy(packet->buffer + packet->start, Util.ObjectToVoidPtr(data), size);

            addHeader(packet, destIP, srcPort, DestPort);

            IPV4.Send(packet, destIP, PROTOCOL_UDP);

            NetPacket.Free(packet);
        }

        /// <summary>
        /// Send UDP packet
        /// </summary>
        /// <param name="packet">Packet structure</param>
        /// <param name="destMac">Destination MAC</param>
        /// <param name="destIP">Destination IP</param>
        /// <param name="srcPort">Source port</param>
        /// <param name="DestPort">Destination port</param>
        public static unsafe void Send(NetPacketDesc* packet, byte[] destIP, ushort srcPort, ushort DestPort)
        {
            /**
             * No support for packets over 1500 bytes
             */
            if (packet->end - packet->start >= 1500)
                return;
            addHeader(packet, destIP, srcPort, DestPort);

            IPV4.Send(packet, destIP, PROTOCOL_UDP);
        }
    }
}
