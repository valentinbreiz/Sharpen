﻿using Sharpen.Mem;
using Sharpen.USB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sharpen.Drivers.USB
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MouseLocation
    {
        public byte Buttons;
        public byte MovementX;
        public byte MovementY;
    }


    unsafe class USBHIDMouse : IUSBDriver
    {


        private USBTransfer* mTransfer;

        private MouseLocation* mLocation;

        public static void Init()
        {
            USBHIDMouse mouse = new USBHIDMouse();
            USBDrivers.RegisterDriver(mouse);
        }

        public void Close(USBDevice device)
        {

        }

        public unsafe IUSBDriver Load(USBDevice device)
        {
            USBInterfaceDescriptor* desc = device.InterfaceDesc;
            if (!(desc->Class == (byte)USBClassCodes.HID &&
                desc->SubClass == 0x01 &&
                desc->Protocol == 0x02))
                return null;

            device.Classifier = USBDeviceClassifier.FUNCTION;

            mTransfer = (USBTransfer*)Heap.Alloc(sizeof(USBTransfer));
            mLocation = (MouseLocation*)Heap.Alloc(sizeof(MouseLocation));

            /**
             * Prepare poll transfer
             */
            mTransfer->Data = (byte *)mLocation;
            mTransfer->Length = 3;
            mTransfer->Executed = false;
            mTransfer->Success = false;
            mTransfer->ID = 2;



            //device.PrepareInterrupt(device, mTransfer);


            return this;
        }

        /// <summary>
        /// Driver poll
        /// </summary>
        /// <param name="device"></param>
        public void Poll(USBDevice device)
        {
            if (mTransfer->Executed)
            {
                if (mTransfer->Success)
                {
                    /**
                     * Todo: Handle mouse events here!
                     */

                    
                }
                
                /**
                 * And again
                 */
                mTransfer->Executed = false;
                device.PrepareInterrupt(device, mTransfer);
            }
        }
    }
}
