﻿using Sharpen.Arch;
using Sharpen.Mem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharpen.Drivers.USB
{
    public unsafe class UHCIDevice : IUSBController
    {
        public USBHelpers.ControllerPoll Poll { get; set; }

        public USBControllerType Type { get { return USBControllerType.UHCI; } }

        public ushort IOBase { get; set; }

        public int* FrameList { get; set; }
    }


    unsafe class UHCI
    {
        const ushort INTF_UHCI = 0x00;

        const ushort REG_USBCMD    = 0x00;
        const ushort REG_USBSTS    = 0x02;
        const ushort REG_USBINTR   = 0x04;
        const ushort REG_FRNUM     = 0x06;
        const ushort REG_FRBASEADD = 0x08;
        const ushort REG_SOFMOD    = 0x0C;
        const ushort REG_PORTSC1   = 0x10;
        const ushort REG_PORTSC2   = 0x12;
        const ushort REG_LEGSUP    = 0xC0;


        const ushort PORTSC_CUR_STAT            = (1 << 0);
        const ushort PORTSC_STAT_CHNG           = (1 << 1);
        const ushort PORTSC_CUR_ENABLE          = (1 << 2);
        const ushort PORTSC_ENABLE_STAT         = (1 << 3);
        const ushort PORTSC_LINE_STAT           = (1 << 4);
        const ushort PORTSC_RESUME_DETECT       = (1 << 6);
        const ushort PORTSC_LOW_SPEED           = (1 << 8);
        const ushort PORTSC_RESET               = (1 << 9);
        const ushort PORTSC_SUSPEND             = (1 << 12);

        const ushort FL_TERMINATE = (1 << 0);
        const ushort FL_QUEUEHEAD = (1 << 1);

        const ushort TD_TERMINATE = (1 << 0);
        const ushort TD_QUEUEHEAD = (1 << 1);
        const ushort TD_DEPTHSEL  = (1 << 2);

        const ushort USBCMD_RS = (1 << 0);
        const ushort USBCMD_HCRESET = (1 << 1);
        const ushort USBCMD_GRESET = (1 << 2);
        const ushort USBCMD_EGSM = (1 << 3);
        const ushort USBCMD_FGR = (1 << 4);
        const ushort USBCMD_SWDBG = (1 << 5);
        const ushort USBCMD_CF = (1 << 6);
        const ushort USBCMD_MAXP = (1 << 7);
        

        struct UHCITransmitDescriptor
        {
            public int Link;
            public int Control;
            public int Token;
            public int BufferPointer;
        }

        struct UHCIQueueHead
        {
            public int Head;
            public int Element;
        }

        /// <summary>
        /// 
        /// </summary>
        public static unsafe void Init()
        {
            /**
             * Note: this cycles through PCI devices!
             */
            for (int i = 0; i < PCI.DeviceNum; i++)
            {
                PciDevice dev = PCI.Devices[i];

                if (dev.CombinedClass == (int)PCIClassCombinations.USBController && dev.ProgIntf == INTF_UHCI)
                    initDevice(dev);
            }

        }

        private static void initDevice(PciDevice dev)
        {
            if ((dev.BAR4.flags & PCI.BAR_IO) == 0)
            {
                Console.WriteLine("[UHCI] Only Portio supported");
            }

            UHCIDevice uhciDev = new UHCIDevice();
            uhciDev.IOBase = (ushort)dev.BAR4.Address;

            Console.Write("[UHCI] Initalize at 0x");
            Console.WriteHex(uhciDev.IOBase);
            Console.WriteLine("");

            uhciDev.FrameList = (int*)Heap.AlignedAlloc(0x1000, sizeof(int) * 1024);

            for (int i = 0; i < 1024; i++)
                uhciDev.FrameList[i] = FL_TERMINATE;

            /**
             * Initalize framelist
             */
            PortIO.Out16((ushort)(uhciDev.IOBase + REG_FRNUM), 0);
            PortIO.Out32((ushort)(uhciDev.IOBase + REG_FRBASEADD), (uint)Paging.GetPhysicalFromVirtual(uhciDev.FrameList));
            PortIO.Out8(((ushort)(uhciDev.IOBase + REG_SOFMOD)), 0x40); // Ensure default value of 64 (aka cycle time of 12000)

            /**
             * We are going to poll!
             */
            PortIO.Out16((ushort)(uhciDev.IOBase + REG_USBINTR), 0x00);

            /**
             * Clear any pending statusses
             */
            PortIO.Out16((ushort)(uhciDev.IOBase + REG_USBSTS), 0xFFFF);

            /**
             * Enable device
             */
            PortIO.Out16((ushort)(uhciDev.IOBase + REG_USBCMD), USBCMD_RS);

            Arch.USB.RegisterController(uhciDev);

            probe(uhciDev);
        }

        /// <summary>
        /// Reset port
        /// </summary>
        /// <param name="port">Port num to reset</param>
        private static void resetPort(UHCIDevice uhciDev, ushort port)
        {
            /**
             * Set reset bit
             */
            setPortBit(uhciDev, port, PORTSC_RESET);

            /**
             * Wait for 60 ms
             */
            Sleep(60);

            /**
             * Unset reset bit
             */
            unsetPortBit(uhciDev, port, PORTSC_RESET);

            /**
             * Wait for atleast 150ms for link to go up
             */
            for(int i =0; i < 15; i++)
            {
                Sleep(10);

                ushort status = PortIO.In16((ushort)(uhciDev.IOBase + port));

                /**
                 * Is it even connected?
                 */
                if(((status) & PORTSC_CUR_STAT) == 0)
                    break;

                /**
                 * Status changed?
                 */
                if(((status) & (PORTSC_STAT_CHNG | PORTSC_ENABLE_STAT)) > 0)
                {
                    unsetPortBit(uhciDev, port, PORTSC_STAT_CHNG | PORTSC_ENABLE_STAT);
                    continue;
                }

                /**
                 * Enabled?
                 */
                if((status & PORTSC_CUR_ENABLE) > 0)
                    break;

            }
        }

        /// <summary>
        /// Sleep for X ms
        /// </summary>
        /// <param name="cnt"></param>
        private static void Sleep(int cnt)
        {
            for (int i = 0; i < cnt; i++)
                PortIO.In32(0x80);
        }


        /// <summary>
        /// Set bit on port
        /// </summary>
        /// <param name="port"></param>
        /// <param name="bit"></param>
        private static void setPortBit(UHCIDevice uhciDev, ushort port, ushort bit)
        {
            ushort status = PortIO.In16((ushort)(uhciDev.IOBase + port));
            status |= bit;
            PortIO.Out16((ushort)(uhciDev.IOBase + port), status);
        }

        /// <summary>
        /// Unset bit on port
        /// </summary>
        /// <param name="port"></param>
        /// <param name="bit"></param>
        private static void unsetPortBit(UHCIDevice uhciDev, ushort port, ushort bit)
        {
            ushort status = PortIO.In16((ushort)(uhciDev.IOBase + port));
            status &= (ushort)~bit;
            PortIO.Out16((ushort)(uhciDev.IOBase + port), status);
        }

        private static void probe(UHCIDevice uhciDev)
        {

            /**
             * UHCI only supports 2 ports, so just 2 :-)
             */
            for(int i = 0; i < 2; i++)
            {
                ushort port = (i == 0 )? REG_PORTSC1 : REG_PORTSC2;

                resetPort(uhciDev, port);


                ushort status = PortIO.In16((ushort)(uhciDev.IOBase + port));

                /**
                 * Is the port even connected?
                 */
                if ((status & PORTSC_CUR_STAT) == 0)
                    continue;

                bool lowSpeed = ((status & PORTSC_LOW_SPEED) > 0);

                /**
                 * TODO: Handle connected device!
                 */

            }
        }
    }
}
