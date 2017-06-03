﻿using Sharpen.Arch;
using Sharpen.Mem;
using Sharpen.MultiTasking;
using Sharpen.Synchronisation;
using Sharpen.USB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sharpen.Drivers.USB
{

    public unsafe class EHCIController : IUSBController
    {

        public USBControllerType Type { get { return USBControllerType.EHCI; } }

        public USBHelpers.ControllerPoll Poll { get; set; }

        public int MemoryBase { get; set; }

        public EHCIHostCapRegister * CapabilitiesRegisters { get; set; }

        public int OperationalRegisters { get; set; }

        public int* FrameList { get; set; }

        public EHCIQueueHead* QueueHeadPool { get; set; }

        public EHCITransferDescriptor* TransferPool { get; set; }
        
        public EHCIQueueHead* FirstHead { get; set; }

        public EHCIQueueHead* AsyncQueueHead { get; set; }

        public EHCIQueueHead* PeriodicQueuehead { get; set; }

        public int PortNum { get; set; }
    }

    public unsafe struct EHCITransferDescriptor
    {
        public int NextLink { get; set; }

        public int Reserved { get; set; }

        public int Token { get; set; }

        public int BufferPointer { get; set; }


        public bool Allocated { get; set; }
        public EHCITransferDescriptor* Previous { get; set; }
        public EHCITransferDescriptor* Next { get; set; }
    }

    public unsafe struct EHCIQueueHead
    {
        public int Head { get; set; }
        public int EPCharacteristics { get; set; }
        public int EPCapabilities { get; set; }

        public int CurLink { get; set; }


        // Transfer descriptor
        public int NextLink { get; set; }

        public int Reserved { get; set; }

        public int Token { get; set; }

        public int BufferPointer { get; set; }

        public bool Allocated { get; set; }
        public USBTransfer* Transfer { get; set; }
        public EHCIQueueHead* Previous { get; set; }
        public EHCIQueueHead* Next { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct EHCIHostCapRegister
    {
        public byte CapLength { get; set; }

        public byte Reserved { get; set; }

        public ushort HCIVersion { get; set; }

        public int HCSParams { get; set; }

        public int HCCParams { get; set; }

        public long HCSPPortroute { get; set; }
    }
    
    public class EHCI
    {
        const ushort MAX_HEADS = 16;
        const ushort MAX_TRANSFERS = 32;

        const ushort INTF_EHCI = 0x20;

        const ushort REG_USBCMD = 0x00;
        const ushort REG_USBSTS = 0x04;
        const ushort REG_USBINTR = 0x08;
        const ushort REG_FRINDEX = 0x0C;
        const ushort REG_CTRLDSSEGMENT = 0x10;
        const ushort REG_PERIODICLISTBASE = 0x14;
        const ushort REG_ASYNCLISTADDR = 0x18;
        const ushort REG_CONFIGFLAG = 0x40;
        const ushort REG_PORTSC = 0x44;

        const int USBCMD_RUN = (1 << 0);
        const int USBCMD_HCRESET = (1 << 1);
        const int USBCMD_FLSize = (2 << 2);
        const int USBCMD_PSE = (1 << 4);
        const int USBCMD_ASE = (1 << 5);
        const int USBCMD_IOAAD = (1 << 6);
        const int USBCMD_LHCR = (1 << 7);
        const int USBCMD_ASPMC = (2 << 8);
        const int USBCMD_ASPME = (1 << 11);
        const int USBCMD_ITC = (7 << 16);

        const ushort ITC_1MICROFRAME = 0x01;
        const ushort ITC_2MICROFRAMES = 0x02;
        const ushort ITC_4MICROFRAMES = 0x04;
        const ushort ITC_8MICROFRAMES = 0x08;
        const ushort ITC_16MICROFRAMES = 0x10;
        const ushort ITC_32MICROFRAMES = 0x20;
        const ushort ITC_64MICROFRAMES = 0x40;

        const int PORTSC_CUR_STAT = (1 << 0);
        const int PORTSC_CON = (1 << 1);
        const int PORTSC_EN = (1 << 2);
        const int PORTSC_EN_CHNG = (1 << 3);
        const int PORTSC_OC_ACT = (1 << 4);
        const int PORTSC_OC_CHNG = (1 << 5);
        const int PORTSC_FP_RESUME = (1 << 6);
        const int PORTSC_SUSPEND = (1 << 7);
        const int PORTSC_RESET = (1 << 8);
        const int PORTSC_LINE_STAT = (2 << 10);
        const int PORTSC_POWER = (1 << 12);
        const int PORTSC_CPC = (1 << 13);
        const int PORTSC_PIC = (2 << 14);
        const int PORTSC_PTC = (0x7 << 16);
        const int PORTSC_WOCE = (1 << 20);
        const int PORTSC_WODE = (1 << 21);
        const int PORTSC_WOOCE = (1 << 22);
        const int PORTSC_CHANGE = (PORTSC_EN_CHNG | PORTSC_OC_CHNG | PORTSC_CON);

        const int INTR_TRANSFER = (1 << 0);
        const int INTR_ERROR = (1 << 1);
        const int INTR_STAT_CHANGE = (1 << 2);
        const int INTR_LFR = (1 << 3);
        const int INTR_HC_ERROR = (1 << 4);
        const int INTR_AA_ENABLE = (1 << 5);

        const uint HCSPARAMS_PORTS_MASK = (0xF << 0);

        const ushort FL_TERMINATE = (1 << 0);
        const ushort FL_QUEUEHEAD = (1 << 1);

        const ushort TD_TERMINATE = (1 << 0);

        private static Mutex mMutex;

        /// <summary>
        /// Initialize
        /// </summary>
        public static unsafe void Init()
        {
            mMutex = new Mutex();
            /**
             * Note: this cycles through PCI devices!
             */
            for (int i = 0; i < Pci.DeviceNum; i++)
            {
                PciDevice dev = Pci.Devices[i];

                if (dev.CombinedClass == (int)PciClassCombinations.USBController && dev.ProgIntf == INTF_EHCI)
                    initDevice(dev);
            }

        }

        /// <summary>
        /// Read ports from controller
        /// </summary>
        /// <param name="controller"></param>
        /// <returns></returns>
        private unsafe static int ReadPorts(EHCIController controller)
        {
            return (int)((*controller.CapabilitiesRegisters).HCSParams & HCSPARAMS_PORTS_MASK);
        }

        private static unsafe void resetPort(EHCIController controller, int portNum)
        {

            /**
             * Set reset bit
             */
            setPortBit(controller, portNum, PORTSC_RESET);

            /**
             * Wait for 60 ms
             */
            Tasking.CurrentTask.CurrentThread.Sleep(0, 60);

            /**
             * Unset reset bit
             */
            unsetPortBit(controller, portNum, PORTSC_RESET);


            /**
             * Wait for atleast 150ms for link to go up
             */
            for (int i = 0; i < 15; i++)
            {
                Tasking.CurrentTask.CurrentThread.Sleep(0, 10);

                int status = *(int*)((controller.OperationalRegisters + REG_PORTSC) + (portNum * 4));

                /**
                 * Is it even connected?
                 */
                if (((status) & PORTSC_CUR_STAT) == 0)
                    break;

                /**
                 * Status changed?
                 */
                if (((status) & (PORTSC_CON | PORTSC_EN_CHNG)) > 0)
                {
                    unsetPortBit(controller, portNum, PORTSC_CON | PORTSC_EN_CHNG);

                    continue;
                }

                /**
                 * Enabled?
                 */
                if ((status & PORTSC_EN) > 0)
                    break;
                
            }
        }


        /// <summary>
        /// Set bit on port
        /// </summary>
        /// <param name="port">Port number</param>
        /// <param name="bit">Bit to setr</param>
        private unsafe static void setPortBit(EHCIController uhciDev, int port, ushort bit)
        {
            int* portAdr = (int*)((uhciDev.OperationalRegisters + REG_PORTSC) + (port * 4));

            int status = *portAdr;
            status |= bit;

            // Reset port changes
            status &= ~PORTSC_CHANGE;

            *portAdr = status;
        }

        /// <summary>
        /// Unset bit on port
        /// </summary>
        /// <param name="port">Port number</param>
        /// <param name="bit">Bit to unset</param>
        private unsafe static void unsetPortBit(EHCIController uhciDev, int port, ushort bit)
        {
            int* portAdr = (int*)((uhciDev.OperationalRegisters + REG_PORTSC) + (port * 4));

            int status = *portAdr;
            status &= ~PORTSC_CHANGE;
            status &= ~bit;

            // Reset port changes
            status |= PORTSC_CHANGE & bit;

            *portAdr = status;
        }


        /// <summary>
        /// Prepare interrupt
        /// </summary>
        /// <param name="dev"></param>
        /// <param name="transfer"></param>
        private unsafe static void PrepareInterrupt(USBDevice dev, USBTransfer* transfer)
        {
            transfer->Executed = true;
            transfer->Success = false;
        }


        /// <summary>
        /// Transfer
        /// </summary>
        /// <param name="dev">Device</param>
        /// <param name="transfer">Transfers</param>
        /// <param name="length">Number of transfers</param>
        private static unsafe void TransferOne(USBDevice dev, USBTransfer* transfer)
        {
            transfer->Executed = true;
            transfer->Success = false;
        }

        /// <summary>
        /// Control USB Device
        /// </summary>
        /// <param name="dev"></param>
        /// <param name="transfer"></param>
        private unsafe static void Control(USBDevice dev, USBTransfer* transfer)
        {
            transfer->Executed = true;
            transfer->Success = false;
        }

        /// <summary>
        /// Probe usb devices on port
        /// </summary>
        /// <param name="uhciDev">The UHCI device</param>
        private static unsafe void probe(EHCIController controller)
        {
            /**
             * UHCI only supports 2 ports, so just 2 :-)
             */
            for (int portNum = 0; portNum < controller.PortNum; portNum++)
            {
                resetPort(controller, portNum);


                int status = *(int*)((controller.OperationalRegisters + REG_PORTSC) + (portNum * 4));

                /**
                 * Is the port even connected?
                 */
                if ((status & PORTSC_CUR_STAT) == 0)
                    continue;
                

                USBDevice dev = new USBDevice();
                dev.Controller = controller;
                dev.Control = Control;
                dev.PrepareInterrupt = PrepareInterrupt;
                dev.TransferOne = TransferOne;

                /**
                 * Root hub
                 */
                dev.Parent = null;
                dev.Port = (uint)portNum;
                dev.State = USBDeviceState.ATTACHED;
                dev.Speed = USBDeviceSpeed.HIGH_SPEED;

                if (!dev.Init())
                {
                    Console.Write("[EHCI] Device init  failed on port ");
                    Console.WriteNum(portNum);
                    Console.WriteLine("");

                    Heap.Free(dev);
                }
            }
        }

        #region Head allocation

        /// <summary>
        /// Get Queue head item
        /// </summary>
        /// <param name="dev">Device</param>
        /// <returns></returns>
        private unsafe static EHCIQueueHead* GetQueueHead(EHCIController dev)
        {
            mMutex.Lock();
            int i = 0;
            while (i < MAX_HEADS)
            {
                if (!dev.QueueHeadPool[i].Allocated)
                {
                    dev.QueueHeadPool[i].Allocated = true;
                    dev.QueueHeadPool[i].Next = null;
                    dev.QueueHeadPool[i].Previous = null;

                    mMutex.Unlock();
                    return (EHCIQueueHead*)(((int)dev.QueueHeadPool) + (sizeof(EHCIQueueHead) * i));
                }

                i++;
            }

            mMutex.Unlock();
            return null;
        }
        #endregion

        private unsafe static EHCIQueueHead *AllocateEmptyQH(EHCIController controller)
        {
            EHCIQueueHead* queueHead = GetQueueHead(controller);
            queueHead->Head = FL_TERMINATE;
            queueHead->EPCapabilities = 0x00;
            queueHead->EPCharacteristics = 0x00;
            queueHead->CurLink = 0x00;
            queueHead->NextLink = 0x00;
            queueHead->Token = 0x00;
            queueHead->BufferPointer = 0x00;
            queueHead->Next = null;
            queueHead->Previous = null;
            queueHead->Transfer = null;

            controller.AsyncQueueHead = queueHead;

            return queueHead;
        }

        private unsafe static void initDevice(PciDevice dev)
        {

            if ((dev.BAR0.flags & Pci.BAR_IO) != 0)
            {
                Console.WriteLine("[EHCI] Only Memory mapped IO supported");
            }

            /**
             * Enable bus mastering
             */
            Pci.EnableBusMastering(dev);

            ulong barAddress = dev.BAR0.Address;
            
            EHCIController controller = new EHCIController();
            controller.MemoryBase = (int)Paging.MapToVirtual(Paging.KernelDirectory, (int)barAddress, 20 * 0x1000, Paging.PageFlags.Writable | Paging.PageFlags.Present);

            controller.FrameList = (int*)Heap.AlignedAlloc(0x1000, sizeof(int) * 1024);
            controller.CapabilitiesRegisters = (EHCIHostCapRegister*)(controller.MemoryBase);
            controller.OperationalRegisters = controller.MemoryBase + (*controller.CapabilitiesRegisters).CapLength;
            controller.PortNum = ReadPorts(controller);
            controller.QueueHeadPool = (EHCIQueueHead*)Heap.AlignedAlloc(0x1000, sizeof(EHCIQueueHead) * MAX_HEADS); 
            controller.TransferPool = (EHCITransferDescriptor*)Heap.AlignedAlloc(0x1000, sizeof(EHCITransferDescriptor) * MAX_TRANSFERS);
            controller.AsyncQueueHead = AllocateEmptyQH(controller);

            // Link to itself
            controller.AsyncQueueHead[0].Head = (int)controller.AsyncQueueHead | FL_QUEUEHEAD;

            controller.PeriodicQueuehead = AllocateEmptyQH(controller);

            for (int i = 0; i < 1024; i++)
                controller.FrameList[i] = FL_QUEUEHEAD | (int)controller.PeriodicQueuehead;

            // Set device
            * (int*)(controller.OperationalRegisters + REG_FRINDEX) = 0;
            * (int *)(controller.OperationalRegisters + REG_PERIODICLISTBASE) = (int)Paging.GetPhysicalFromVirtual(controller.FrameList);
            *(int*)(controller.OperationalRegisters + REG_ASYNCLISTADDR) = (int)Paging.GetPhysicalFromVirtual(controller.AsyncQueueHead);
            *(int*)(controller.OperationalRegisters + REG_CTRLDSSEGMENT) = 0;
            
            // Reset status
            *(int*)(controller.OperationalRegisters + REG_USBSTS) = 0x3F;


            // enable device
            *(int*)(controller.OperationalRegisters + REG_USBCMD) = USBCMD_PSE | USBCMD_RUN | USBCMD_ASPME | (ITC_8MICROFRAMES << USBCMD_ITC);

            // Wait till done
            while ((*(int*)(controller.OperationalRegisters + REG_USBSTS) & (1 << 12)) > 0)
                CPU.HLT();

            Console.Write("[EHCI] Detected with ");
            Console.WriteHex(controller.PortNum);
            Console.WriteLine(" ports");

            probe(controller);
        }
    }
}