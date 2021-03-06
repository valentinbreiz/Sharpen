﻿using Sharpen.Arch;
using Sharpen.Mem;
using Sharpen.Net;
using Sharpen.MultiTasking;
using Sharpen.Utilities;

namespace Sharpen.Drivers.Net
{
    unsafe class E1000
    {
        private const ushort NUM_RX_DESCRIPTORS  = 32;
        private const ushort NUM_TX_DESCRIPTIORS = 32;

        /**
        * Device ids
        */
        private const ushort MANUID_INTEL    = 0x8086;
        private const ushort DEVID_EMU       = 0x100E;
        private const ushort DEVID_I217      = 0x153A;
        private const ushort DEVID_82577LM   = 0x10EA;
        private const ushort DEVID_82545EM   = 0x100F;
        private const ushort DEVID_82545EMA  = 0x100;
        private const ushort DEVID_82545EMAF = 1011;

        /**
         * Registers (incomplete)
         */
        private const ushort REG_CTRL       = 0x00;
        private const ushort REG_STATUS     = 0x08;
        private const ushort REG_EECD       = 0x10;
        private const ushort REG_EERD       = 0x14;
        private const ushort REG_FLA        = 0x1C;
        private const ushort REG_CTRL_EXT   = 0x18;
        private const ushort REG_MDIC       = 0x20;
        private const ushort REG_ICR        = 0xC0;
        private const ushort REG_LEDCTL     = 0xE00;
        private const ushort REG_MULTICAST  = 0x5200;
        private const ushort REG_IMASK      = 0x00D0;
        private const ushort REG_RCTL       = 0x0100;
        private const ushort REG_RDBAL      = 0x2800;
        private const ushort REG_RDBAH      = 0x2804;
        private const ushort REG_RDLEN      = 0x2808;
        private const ushort REG_RDH        = 0x2810;
        private const ushort REG_RDT        = 0x2818;

        private const ushort REG_TDBAL = 0x3800;
        private const ushort REG_TDBAH = 0x3804;
        private const ushort REG_TDLEN = 0x3808;
        private const ushort REG_TDH   = 0x3810;
        private const ushort REG_TDT   = 0x3818;
        private const ushort REG_TCTL  = 0x0400;

        private const ushort REG_RD_DD  = (1 << 0);
        private const ushort REG_RD_EOP = (1 << 1);
        
        /**
         * EEP REQ bits
         */
        private const ushort REG_EEP_SK     = (1 << 0);
        private const ushort REG_EEP_CS     = (1 << 1);
        private const ushort REG_EEP_DI     = (1 << 2);
        private const ushort REG_EEP_DO     = (1 << 3);
        private const ushort REG_EEP_FWE    = (1 << 4) | (1 << 5);
        private const ushort REG_EEP_REQ    = (1 << 6);
        private const ushort REG_EEP_GNT    = (1 << 7);
        private const ushort REG_EEP_PRES   = (1 << 8);
        private const ushort REG_EEP_SIZE   = (1 << 9);
        private const ushort REG_EEP_SIZE2  = (1 << 10);
        private const ushort REG_EEP_TYPE   = (1 << 13);

        /**
         * Control register
         */
        private const ushort REG_CTRL_FD     = (1 << 0);
        private const ushort REG_CTRL_LRST   = (1 << 3);
        private const ushort REG_CTRL_ASDE   = (1 << 5);
        private const ushort REG_CTRL_SLU    = (1 << 6);
        private const ushort REG_CTRL_ILOS   = (1 << 7);
        private const ushort REG_CTRL_SPEED  = (1 << 8) | (1 << 9);
        private const ushort REG_CTRL_FRCSPD = (1 << 11);

        /**
         * EERD registers
         */
        private const ushort REG_EERD_START = (1 << 0);
        private const ushort REG_EERD_DONE  = (1 << 4);

        /**
         * Read control register
         */
        private const uint REG_RCT_SBP     = (1 << 2);
        private const uint REG_RCT_UPE     = (1 << 3);
        private const uint REG_RCT_MPE     = (1 << 4);
        private const uint REG_RCT_LPE     = (1 << 5);
        private const uint REG_RCT_RDMTS   = (1 << 8);
        private const uint REG_RCT_BAM     = (1 << 15);
        private const uint REG_RCTL_BSIZE  = (1 << 16);
        private const uint REG_RCTL_BSEX   = (1 << 25);
        private const uint REG_RCTL_BSECRC = (1 << 26);

        /**
         * Interrupt status register
         */
        private const uint REG_RXSEQ = (1 << 3);
        private const uint REG_RXO   = (1 << 6);
        private const uint REG_RXT0  = (1 << 7);

        /**
         * Receive descriptor cmd
         */
        private const byte REG_RCMD_EOP  = (1 << 0);
        private const byte REG_RCMD_IFCS = (1 << 1);
        private const byte REG_RCMD_IC   = (1 << 2);

        /**
         * Transmit control register
         */
        private const ushort REG_TCTL_EN  = (1 << 1);
        private const ushort REG_TCTL_PSP = (1 << 2);

        /**
         * We should do this in the PCI driver!
         */
        private const ushort PCI_MEM  = (1 << 0);
        private const ushort PCI_SIZE = (1 << 1) | (1 << 2);

        private static byte[] m_mac;

        /**
         * Register bases
         */
        private static uint m_register_base;
        private static uint m_flash_base;
        private static uint m_io_base;

        private static ushort m_irq_num;

        private static RX_DESC* m_rx_descs;
        private static TX_DESC* m_tx_descs;

        private static byte*[] m_rx_buffers;
        private static byte*[] m_tx_buffers;
        private static uint m_rx_next = 0;
        private static uint m_tx_next = 0;
        private static byte[] m_packetBuffer;

        private static volatile uint m_linkup = 0;

        /// <summary>
        /// Receive descriptor
        /// </summary>
        private struct RX_DESC
        {
            public ulong Address;
            public ushort Length;
            public ushort Checksum;
            public byte Status;
            public byte Errors;
            public ushort Special;
        }
        
        /// <summary>
        /// Transmit descriptor
        /// </summary>
        private struct TX_DESC
        {
            public ulong Address;
            public ushort Length;
            public byte CSO;
            public byte CMD;
            public byte STA;
            public byte CSS;
            public ushort Special;
        }

        /// <summary>
        /// Read from EEP
        /// </summary>
        /// <param name="adr">EEP address</param>
        /// <returns></returns>
        private unsafe static ushort eepRead(uint adr)
        {
            /**
             * Start read, and write address
             */
            uint *ptr = (uint*)(m_register_base + REG_EERD);

            *ptr = (REG_EERD_START) | (adr << 8);


            /**
             * Wait till done
             */
            while ((*ptr & REG_EERD_DONE) == 0)
                Tasking.Yield();

            return (ushort)((*ptr >> 16) & 0xFFFF);
        }

        /// <summary>
        /// Read mac address from device
        /// </summary>
        private static void readMac()
        {
            m_mac = new byte[6];

            ushort tmp = eepRead(0);
            m_mac[0] = (byte)(tmp & 0xFF);
            m_mac[1] = (byte)((tmp >> 8) & 0xFF);
            tmp = eepRead(1);
            m_mac[2] = (byte)(tmp & 0xFF);
            m_mac[3] = (byte)((tmp >> 8) & 0xFF);
            tmp = eepRead(2);
            m_mac[4] = (byte)(tmp & 0xFF);
            m_mac[5] = (byte)((tmp >> 8) & 0xFF);
        }

        /// <summary>
        /// PCI init handler
        /// </summary>
        /// <param name="dev"></param>
        private static unsafe void initHandler(PciDevice dev)
        {
            m_register_base = (uint)dev.BAR0.Address;
            m_flash_base = (uint)dev.BAR1.Address;
            
            /**
             * Check if there is a memory bar
             */
            if ((dev.BAR0.flags & Pci.BAR_IO) > 0)
            {
                Console.WriteLine("[E1000] Device not MMIO!");
                return;
            }

            m_packetBuffer = new byte[9500];
            
            /**
             * Enable bus mastering
             */
            Pci.EnableBusMastering(dev);
            
            /**
             * Map device
             */
            m_register_base = (uint)Paging.MapToVirtual(Paging.KernelDirectory, (int)m_register_base, 20 * 0x1000, Paging.PageFlags.Writable | Paging.PageFlags.Present);
            Pci.SetInterruptHandler(dev, handler);
            
            readMac();
            start();

            /**
             * Waiting for link to go up
             */
            Console.WriteLine("[E1000] Waiting for link to go up....");
            while (m_linkup == 0)
                CPU.HLT();

            /**
             * Register device as the main network device
             */
            Network.NetDevice netDev = new Network.NetDevice();
            netDev.ID = dev.Device;
            netDev.Transmit = Transmit;
            netDev.GetMac = GetMac;

            Network.Set(netDev);
        }

        /// <summary>
        /// Start E1000
        /// </summary>
        private static unsafe void start()
        {
            setInterruptMask();
            linkUp();
            
            // Clearout multicast filter
            for (int i = 0; i < 0x80; i++)
            {
                *(uint*)(m_register_base + REG_MULTICAST + (i * 4)) = 0;
            }
            
            rxInit();
            txInit();
        }

        /// <summary>
        /// Set interrupt mask
        /// </summary>
        private static unsafe void setInterruptMask()
        {
            *(uint*)(m_register_base + REG_IMASK) = 0xFF;
        }

        /// <summary>
        /// Set link up
        /// </summary>
        private static unsafe void linkUp()
        {
            *(uint*)(m_register_base + REG_CTRL) |= REG_CTRL_SLU;
        }

        /// <summary>
        /// Initalize RXs
        /// </summary>
        private static unsafe void rxInit()
        {
            /**
             * Allocate receive descriptors
             */
            m_rx_descs = (RX_DESC*)Heap.AlignedAlloc(16, NUM_RX_DESCRIPTORS * sizeof(RX_DESC));
            m_rx_buffers = new byte*[NUM_RX_DESCRIPTORS];
            
            for (int i = 0; i < NUM_RX_DESCRIPTORS; i++)
            {
                m_rx_buffers[i] = (byte*)Heap.AlignedAlloc(16, 8192);
                m_rx_descs[i].Address = (uint)(Paging.GetPhysicalFromVirtual(m_rx_buffers[i]));
                m_rx_descs[i].Status = 0;
            }

            /**
             * Set rx address to device
             */
            *(uint*)(m_register_base + REG_RDBAL) = (uint)Paging.GetPhysicalFromVirtual(m_rx_descs);
            *(uint*)(m_register_base + REG_RDBAH) = 0;

            /**
             * Setup total length
             */
            *(uint*)(m_register_base + REG_RDLEN) = NUM_RX_DESCRIPTORS * (uint)sizeof(RX_DESC);
            *(uint*)(m_register_base + REG_RDH) = 0;
            *(uint*)(m_register_base + REG_RDT) = NUM_RX_DESCRIPTORS - 1;

            /**
             * Setup read control register
             */
            *(uint*)(m_register_base + REG_RCTL) = REG_RCTL_BSEX | REG_RCTL_BSECRC | REG_RCT_BAM | REG_RCT_LPE | (1 << 1) | REG_RCT_SBP | (2 << 16);
        }
        
        /// <summary>
        /// Transmit packet
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="size"></param>
        public static unsafe void Transmit(byte* bytes, uint size)
        {
            if (size > 8000)
                return;
            
            uint index = m_tx_next++;
            if (m_tx_next == NUM_TX_DESCRIPTIORS)
                m_tx_next = 0;
            
            Memory.Memcpy(m_tx_buffers[index], bytes, (int)size);
            
            m_tx_descs[index].Length = (ushort)size;
            m_tx_descs[index].CSO = 0;
            m_tx_descs[index].CMD = REG_RCMD_EOP | REG_RCMD_IC | REG_RCMD_IFCS;
            m_tx_descs[index].STA = 0;
            m_tx_descs[index].CSO = 0;
            m_tx_descs[index].Special = 0;
            
            *(uint*)(m_register_base + REG_TDT) = m_tx_next;
        }

        /// <summary>
        /// Initalize TX
        /// </summary>
        private static unsafe void txInit()
        {
            /**
             * Allocate transmit descriptors
             */
            m_tx_descs = (TX_DESC*)Heap.AlignedAlloc(16, NUM_TX_DESCRIPTIORS * sizeof(TX_DESC));
            m_tx_buffers = new byte*[NUM_TX_DESCRIPTIORS];

            for (int i = 0; i < NUM_TX_DESCRIPTIORS; i++)
            {
                m_tx_buffers[i] = (byte*)Heap.AlignedAlloc(16, 8192);
                m_tx_descs[i].Address = (uint)Paging.GetPhysicalFromVirtual(m_tx_buffers[i]);
                m_tx_descs[i].CMD = 0;
                m_tx_descs[i].CSO = 0;
                m_tx_descs[i].CSS = 0;
                m_tx_descs[i].Length = 0;
                m_tx_descs[i].Special = 0;
                m_tx_descs[i].STA = 0;
            }

            /**
             * Set tx address to device
             */
            *(uint*)(m_register_base + REG_TDBAL) = (uint)Paging.GetPhysicalFromVirtual(m_tx_descs);
            *(uint*)(m_register_base + REG_TDBAH) = 0;

            /**
             * Setup total length
             */
            *(uint*)(m_register_base + REG_TDLEN) = NUM_TX_DESCRIPTIORS * (uint)sizeof(TX_DESC);
            *(uint*)(m_register_base + REG_TDH) = 0;
            *(uint*)(m_register_base + REG_TDT) = 0;
            
            /**
             * Setup transmit control register
             */
            *(uint*)(m_register_base + REG_TCTL) = REG_TCTL_EN | REG_TCTL_PSP;

        }
        

        /// <summary>
        /// Handle packet reception
        /// </summary>
        private static void receive()
        {
            while ((m_rx_descs[m_rx_next].Status & REG_RD_DD) > 0)
            {
                uint cur = m_rx_next++;
                if (m_rx_next == NUM_RX_DESCRIPTORS)
                    m_rx_next = 0;

                if ((m_rx_descs[cur].Status & REG_RD_EOP) > 0)
                {
                    ushort len = m_rx_descs[cur].Length;

                    Memory.Memcpy(Util.ObjectToVoidPtr(m_packetBuffer), m_rx_buffers[cur], len);
                    
                    Network.QueueReceivePacket(m_packetBuffer, len);
                }

                m_rx_descs[cur].Status = 0;

                *(uint*)(m_register_base + REG_RDT) = cur;
            }
        }

        /// <summary>
        /// Handle interrupt
        /// </summary>
        /// <returns></returns>
        private static unsafe bool handler()
        {
            /**
             * Read Interrupt control state
             */
            uint icr = *(uint*)(m_register_base + REG_ICR);
            if (icr == 0)
                return false;
            
            /**
             * Link status change or transmit empty? then say link is up!
             */
            if ((icr & 0x4) > 0 || (icr & 0x80) > 0)
                m_linkup = 1;
            
            if ((icr & REG_RXSEQ) > 0)
                linkUp();
            
            /**
             * Did we receive a packet
             */
            if ((icr & REG_RXT0) > 0)
            {
                receive();
            }

            //if ((icr & REG_RXO) > 0)
            //    Console.WriteLine("Link still ok :)");

            //Console.Write("ICR: ");
            //Console.WriteHex(icr);
            //Console.WriteLine("");

            return true;
        }

        /// <summary>
        /// Get mac address implementation
        /// </summary>
        /// <param name="mac">Pointer to write mac address to</param>
        private static unsafe void GetMac(byte* mac)
        {
            for (int i = 0; i < 6; i++)
                mac[i] = m_mac[i];
        }

        /// <summary>
        /// Exit handler
        /// </summary>
        /// <param name="dev">The PCI device</param>
        private static void exitHandler(PciDevice dev)
        {
        }

        /// <summary>
        /// Register driver
        /// </summary>
        public static void Init()
        {
            Pci.PciDriver driver = new Pci.PciDriver();
            driver.Name = "E1000 Driver";
            driver.Exit = exitHandler;
            driver.Init = initHandler;

            Pci.RegisterDriver(MANUID_INTEL, DEVID_EMU, driver);
            Pci.RegisterDriver(MANUID_INTEL, DEVID_82545EM, driver);
            Pci.RegisterDriver(MANUID_INTEL, DEVID_82545EMA, driver);
            Pci.RegisterDriver(MANUID_INTEL, DEVID_82545EMAF, driver);
        }
    }
}
