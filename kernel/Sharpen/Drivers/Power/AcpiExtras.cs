﻿using System.Runtime.InteropServices;

namespace Sharpen.Drivers.Power
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct RDSTH
    {
        public fixed char Signature[4];
        public uint Length;
        public byte Revision;
        public byte Checksum;

        public fixed char OEMID[6];

        public fixed char OEMTableID[8];
        public uint OEMRevision;
        public uint CreatorID;
        public uint CreatorRevision;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct RDSP
    {
        public fixed char Signature[8];
        public byte Checksum;
        public fixed char OEMID[6];
        public byte Revision;
        public uint RsdtAddress;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct GenericAddressStructure
    {
        public byte AddressSpace;
        public byte BitWidth;
        public byte BitOffset;
        public byte AccessSize;
        public ulong Address;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct RSDT
    {
        public RDSTH header;
        public uint firstSDT;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct FADT
    {
        public RDSTH Header;
        public uint FirmwareCtrl;
        public uint Dsdt;

        // compatibility with ACPI 1.0
        public byte Reserved;

        byte PreferredPowerManagementProfile;
        public ushort SCI_Interrupt;
        public uint SMI_CommandPort;
        public byte AcpiEnable;
        public byte AcpiDisable;
        public byte S4BIOS_REQ;
        public byte PSTATE_Control;
        public uint PM1aEventBlock;
        public uint PM1bEventBlock;
        public uint PM1aControlBlock;
        public uint PM1bControlBlock;
        public uint PM2ControlBlock;
        public uint PMTimerBlock;
        public uint GPE0Block;
        public uint GPE1Block;
        public byte PM1EventLength;
        public byte PM1ControlLength;
        public byte PM2ControlLength;
        public byte PMTimerLength;
        public byte GPE0Length;
        public byte GPE1Length;
        public byte GPE1Base;
        public byte CStateControl;
        public ushort WorstC2Latency;
        public ushort WorstC3Latency;
        public ushort FlushSize;
        public ushort FlushStride;
        public byte DutyOffset;
        public byte DutyWidth;
        public byte DayAlarm;
        public byte MonthAlarm;
        public byte Century;

        // reserved in ACPI 1.0; used since ACPI 2.0+
        public ushort BootArchitectureFlags;

        public byte Reserved2;
        public uint Flags;

        // 12 byte structure; see below for details
        public GenericAddressStructure ResetReg;

        public byte ResetValue;
        public fixed byte Reserved3[3];

        // 64bit pointers - Available on ACPI 2.0+
        public ulong X_FirmwareControl;
        public ulong X_Dsdt;

        public GenericAddressStructure X_PM1aEventBlock;
        public GenericAddressStructure X_PM1bEventBlock;
        public GenericAddressStructure X_PM1aControlBlock;
        public GenericAddressStructure X_PM1bControlBlock;
        public GenericAddressStructure X_PM2ControlBlock;
        public GenericAddressStructure X_PMTimerBlock;
        public GenericAddressStructure X_GPE0Block;
        public GenericAddressStructure X_GPE1Block;
    }
}