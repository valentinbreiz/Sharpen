﻿using Sharpen.Arch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharpen.Drivers.Block
{
    class AHCI
    {

        public static void Init()
        {
            if(findAHCIDevice() != null)
            {
                Console.WriteLine("[AHCI] Device detected, pauzing kernel...");
                for (;;) ;
            }
            

        }

        private static PciDevice findAHCIDevice()
        {
            /**
             * Note: this cycles through PCI devices!
             */
            for (int i = 0; i < PCI.DeviceNum; i++)
            {
                PciDevice dev = PCI.Devices[i];

                if (dev.CombinedClass == 0x0106 && dev.ProgIntf == 0x01)
                    return dev;
            }

            return null;
        }
    }
}