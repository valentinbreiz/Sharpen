﻿using Sharpen.Arch;

namespace Sharpen.Drivers.Block
{
    unsafe partial class ATA
    {
        #region ATA data

        public static readonly ushort ATA_PRIMARY_IO = 0x1F0;
        public static readonly ushort ATA_SECONDARY_IO = 0x170;

        public static readonly byte ATA_PRIMARY = 0x00;
        public static readonly byte ATA_SECONDARY = 0x01;

        public static readonly byte ATA_MASTER = 0x00;
        public static readonly byte ATA_SLAVE = 0x01;

        public static readonly byte ATA_REG_DATA = 0x00;
        public static readonly byte ATA_REG_ERROR = 0x01;
        public static readonly byte ATA_REG_FEATURE = 0x01;
        public static readonly byte ATA_REG_SECCNT = 0x02;
        public static readonly byte ATA_REG_LBALO = 0x03;
        public static readonly byte ATA_REG_LBAMID = 0x04;
        public static readonly byte ATA_REG_LBAHI = 0x05;
        public static readonly byte ATA_REG_DRIVE = 0x06;
        public static readonly byte ATA_REG_CMD = 0x07;
        public static readonly byte ATA_REG_STATUS = 0x07;
        public static readonly byte ATA_REG_SECCOUNT1 = 0x08;
        public static readonly byte ATA_REG_LBA3 = 0x09;
        public static readonly byte ATA_REG_LBA4 = 0x0A;
        public static readonly byte ATA_REG_LBA5 = 0x0B;
        public static readonly byte ATA_REG_CONTROL = 0x0C;
        public static readonly byte ATA_REG_ALTSTATUS = 0x0C;
        public static readonly byte ATA_REG_DEVADDRESS = 0x0D;

        public static readonly byte ATA_CMD_IDENTIFY = 0xEC;
        public static readonly byte ATA_CMD_PIO_READ = 0x20;
        public static readonly byte ATA_CMD_PIO_WRITE = 0x30;
        public static readonly byte ATA_CMD_FLUSH = 0xE7;

        public static readonly byte ATA_NO_DISK = 0x00;

        public static readonly byte ATA_STATUS_BSY = 0x80;
        public static readonly byte ATA_STATUS_ERR = 0x01;
        public static readonly byte ATA_STATUS_RDY = (1 << 4);
        public static readonly byte ATA_STATUS_SRV = 0x10;
        public static readonly byte ATA_STATUS_DRQ = 0x08;
        public static readonly byte ATA_STATUS_DF = 0x20;

        public static readonly byte ATA_FEATURE_PIO = 0x00;
        public static readonly byte ATA_FEATURE_DMA = 0x01;

        public static readonly byte ATA_IDENT_DEVICETYPE = 0x00;
        public static readonly byte ATA_IDENT_CYLINDERS = 0x6C;
        public static readonly byte ATA_IDENT_HEADS = 0x6E;
        public static readonly byte ATA_IDENT_SECTORSPT = 0x70;
        public static readonly byte ATA_IDENT_SERIAL = 0x14;
        public static readonly byte ATA_IDENT_MODEL = 0x36;
        public static readonly byte ATA_IDENT_CAPABILITIES = 0x62;
        public static readonly byte ATA_IDENT_FIELDVALID = 0x6A;
        public static readonly byte ATA_IDENT_MAX_LBA = 0x78;
        public static readonly byte ATA_IDENT_COMMANDSETS = 0xA4;
        public static readonly byte ATA_IDENT_MAX_LBA_EXT = 0xC8;

        #endregion

        public static IDE_Device[] Devices { get; private set; } = new IDE_Device[4];

        /// <summary>
        /// Waits 400 ns on an ATA device
        /// </summary>
        /// <param name="port">The base IO port</param>
        private static void wait400ns(uint port)
        {
            PortIO.In8((ushort)(port + ATA_REG_ALTSTATUS));
            PortIO.In8((ushort)(port + ATA_REG_ALTSTATUS));
            PortIO.In8((ushort)(port + ATA_REG_ALTSTATUS));
            PortIO.In8((ushort)(port + ATA_REG_ALTSTATUS));
        }

        /// <summary>
        /// Select IDE drive
        /// </summary>
        /// <param name="channel">Channel slave of master?</param>
        /// <param name="drive">Drive slave of master?</param>
        private static void select_drive(byte channel, byte drive)
        {
            if (channel == ATA_PRIMARY)
            {
                if (drive == ATA_MASTER)
                {
                    PortIO.Out8((ushort)(ATA_PRIMARY_IO + ATA_REG_DRIVE), 0xA0);
                }
                else
                {
                    PortIO.Out8((ushort)(ATA_PRIMARY_IO + ATA_REG_DRIVE), 0xB0);
                }
            }
            else
            {
                if (drive == ATA_MASTER)
                {
                    PortIO.Out8((ushort)(ATA_SECONDARY_IO + ATA_REG_DRIVE), 0xA0);
                }
                else
                {
                    PortIO.Out8((ushort)(ATA_SECONDARY_IO + ATA_REG_DRIVE), 0xB0);
                }
            }
        }

        /// <summary>
        /// IDE idenitify
        /// </summary>
        /// <param name="channel">Channel</param>
        /// <param name="drive">Slave or master?</param>
        /// <returns></returns>
        private static byte[] Identify(byte channel, byte drive)
        {
            // Select correct drive
            select_drive(channel, drive);

            // Select base port for ATA drive
            ushort port;
            if (channel == ATA_PRIMARY)
            {
                port = ATA_PRIMARY_IO;
            }
            else
            {
                port = ATA_SECONDARY_IO;
            }

            // Set to first LBA
            PortIO.Out8((ushort)(port + ATA_REG_SECCNT), 0x00);
            PortIO.Out8((ushort)(port + ATA_REG_LBALO), 0x00);
            PortIO.Out8((ushort)(port + ATA_REG_LBAMID), 0x00);
            PortIO.Out8((ushort)(port + ATA_REG_LBAHI), 0x00);

            PortIO.Out8((ushort)(port + ATA_REG_CMD), ATA_CMD_IDENTIFY);

            // Check if a drive is found
            byte status = PortIO.In8((ushort)(port + ATA_REG_STATUS));
            if (status == 0)
            {
                return null;
            }

            // Wait until drive is not busy anymore
            do
            {
                status = PortIO.In8((ushort)(port + ATA_REG_STATUS));
            }
            while ((status & ATA_STATUS_BSY) != 0);

            while (true)
            {
                status = PortIO.In8((ushort)(port + ATA_REG_STATUS));

                if ((status & ATA_STATUS_ERR) != 0)
                {
                    return null;
                }

                if ((status & ATA_STATUS_DRQ) != 0)
                {
                    break;
                }
            }

            // Read data from ATA drive
            byte[] ide_buf = new byte[256];
            int offset = 0;
            for (int i = 0; i < 128; i++)
            {
                ushort shrt = PortIO.In16((ushort)(port + ATA_REG_DATA));
                ide_buf[offset + 0] = (byte)(shrt >> 8);
                ide_buf[offset + 1] = (byte)(shrt);
                offset += 2;
            }

            return ide_buf;
        }

        /// <summary>
        /// Wait for drive to be finished
        /// </summary>
        /// <param name="port">Port IO base</param>
        private static void Poll(uint port)
        {
            wait400ns(port);

            byte status;
            do
            {
                status = PortIO.In8((ushort)(port + ATA_REG_STATUS));
            }
            while ((status & ATA_STATUS_BSY) > 0);

            while ((status & ATA_STATUS_DRQ) == 0)
            {
                status = PortIO.In8((ushort)(port + ATA_REG_STATUS));

                if ((status & ATA_STATUS_DF) > 0)
                {
                    Panic.DoPanic("Device fault!");
                }

                if ((status & ATA_STATUS_ERR) > 0)
                {
                    Panic.DoPanic("ERR IN ATA!!");
                }
            }
        }

        /// <summary>
        /// Read sectors into the output buffer and return size in bytes
        /// </summary>
        /// <param name="lba">Input LBA</param>
        /// <param name="size">Size in sectors</param>
        /// <param name="buffer">Output buffer</param>
        /// <returns>The amount of bytes read</returns>
        public static int ReadSector(uint device_num, uint lba, byte size, byte[] buffer)
        {
            // The driver only supports up to 4 drives
            if (device_num >= 4)
            {
                return 0;
            }

            // Get IDE device from array
            IDE_Device device = Devices[device_num];

            // Does the drive exist?
            if (!device.Exists)
            {
                return 0;
            }

            uint port = device.BasePort;
            int drive = device.Drive;

            int cmd = (drive == ATA_MASTER) ? 0xE0 : 0xF0;

            // Set Drive
            PortIO.Out8((ushort)(port + ATA_REG_DRIVE), (byte)(cmd | (byte)((lba >> 24) & 0x0F)));

            // Set PIO MODE
            PortIO.Out8((ushort)(port + ATA_REG_FEATURE), ATA_FEATURE_PIO);

            // Set size
            PortIO.Out8((ushort)(port + ATA_REG_SECCNT), size);

            // Set LBA
            PortIO.Out8((ushort)(port + ATA_REG_LBALO), (byte)lba);
            PortIO.Out8((ushort)(port + ATA_REG_LBAMID), (byte)(lba >> 8));
            PortIO.Out8((ushort)(port + ATA_REG_LBAHI), (byte)(lba >> 16));

            // Issue command
            PortIO.Out8((ushort)(port + ATA_REG_CMD), ATA_CMD_PIO_READ);

            // Wait till done
            Poll(port);

            // Read data
            int offset = 0;
            for (int i = 0; i < size * 256; i++)
            {
                ushort data = PortIO.In16((ushort)(port + ATA_REG_DATA));
                buffer[offset + 0] = (byte)(data);
                buffer[offset + 1] = (byte)(data >> 8);
                offset += 2;
            }

            return size * 512;
        }

        /// <summary>
        /// Write sector to drive and return size in bytes
        /// </summary>
        /// <param name="lba">Input LBA</param>
        /// <param name="size">Output size in sectors</param>
        /// <param name="buffer">Input buffer</param>
        /// <returns>The amount of bytes written</returns>
        public static int WriteSector(uint device_num, uint lba, byte size, byte[] buffer)
        {
            // The driver only supports up to 4 drivers
            if (device_num >= 4)
            {
                return 0;
            }

            // Get IDE device from array
            IDE_Device device = Devices[device_num];

            // Does the drive exist?
            if (!device.Exists)
            {
                return 0;
            }

            uint port = device.BasePort;
            int drive = device.Drive;

            int cmd = (drive == ATA_MASTER) ? 0xE0 : 0xF0;

            // Set Drive
            PortIO.Out8((ushort)(port + ATA_REG_DRIVE), (byte)(cmd | (byte)((lba >> 24) & 0x0F)));

            // Set PIO MODE
            PortIO.Out8((ushort)(port + ATA_REG_FEATURE), ATA_FEATURE_PIO);

            // Set size
            PortIO.Out8((ushort)(port + ATA_REG_SECCNT), size);

            // Set LBA
            PortIO.Out8((ushort)(port + ATA_REG_LBALO), (byte)lba);
            PortIO.Out8((ushort)(port + ATA_REG_LBAMID), (byte)(lba >> 8));
            PortIO.Out8((ushort)(port + ATA_REG_LBAHI), (byte)(lba >> 16));

            // Issue command
            PortIO.Out8((ushort)(port + ATA_REG_CMD), ATA_CMD_PIO_WRITE);

            // Wait till done
            Poll(port);

            // Wait for 400ns
            wait400ns(port);

            // Write data
            for (int i = 0; i < size * 256; i++)
            {
                int pos = i * 2;
                ushort shrt = (ushort)((buffer[pos + 1] << 8) | buffer[pos]);

                PortIO.Out16((ushort)(port + ATA_REG_DATA), shrt);
            }

            // Flush data
            PortIO.Out8((ushort)(port + ATA_REG_CMD), ATA_CMD_FLUSH);

            // Wait till done
            byte status;
            do
            {
                status = PortIO.In8((ushort)(port + ATA_REG_STATUS));
            }
            while ((status & ATA_STATUS_BSY) > 0);

            return size * 512;
        }

        /// <summary>
        /// IDE Prope
        /// </summary>
        public static void Probe()
        {
            int num = 0;

            // Let's prope 4 devices!
            while (num < 4)
            {
                ushort port;
                byte channel;
                byte drive;

                if (num <= 1)
                {
                    port = ATA_PRIMARY_IO;
                    channel = ATA_PRIMARY;
                }
                else
                {
                    port = ATA_SECONDARY_IO;
                    channel = ATA_SECONDARY;
                }

                if ((num % 2) != 0)
                {
                    drive = ATA_SLAVE;
                }
                else
                {
                    drive = ATA_PRIMARY;
                }

                Devices[num] = new IDE_Device();

                Devices[num].BasePort = port;
                Devices[num].Channel = channel;
                Devices[num].Drive = drive;

                byte[] result = Identify(channel, drive);

                if (result == null)
                {
                    Devices[num].Exists = false;
                    num++;
                    continue;
                }

                Devices[num].Exists = true;

                int pos = ATA_IDENT_COMMANDSETS;
                Devices[num].CmdSet = (uint)((result[pos] << 24) | (result[pos + 1] << 16) | (result[pos + 2] << 8) | result[pos + 3]);

                pos = ATA_IDENT_DEVICETYPE;
                Devices[num].Type = (ushort)((result[pos + 1] << 8) | result[pos]);

                pos = ATA_IDENT_CAPABILITIES;
                Devices[num].Capabilities = (ushort)((result[pos + 1] << 8) | result[pos]);

                pos = ATA_IDENT_CYLINDERS;
                Devices[num].Cylinders = (ushort)((result[pos + 1] << 8) | result[pos]);

                pos = ATA_IDENT_HEADS;
                Devices[num].Heads = (ushort)((result[pos + 1] << 8) | result[pos]);

                pos = ATA_IDENT_SECTORSPT;
                Devices[num].Sectorspt = (ushort)((result[pos + 1] << 8) | result[pos]);

                pos = ATA_IDENT_MAX_LBA;
                Devices[num].Size = (uint)(((result[pos] << 24) | (result[pos + 1] << 16) | (result[pos + 2] << 8) | result[pos + 3]));

                // Model name
                pos = ATA_IDENT_MODEL;

                // NULL-terminated string
                Devices[num].Name = (char*)Heap.Alloc(40 + 1);
                fixed(void* source = &result[pos])
                {
                    Memory.Memcpy(Devices[num].Name, source, 40);
                }
                Devices[num].Name[40] = '\0';
                
                num++;
            }
        }

        /// <summary>
        /// Test write code
        /// </summary>
        public static void WriteTest()
        {
            byte[] buf = new byte[512];
            buf[0] = (byte)Time.Hours;
            buf[1] = (byte)Time.Minutes;
            buf[2] = (byte)Time.Seconds;

            int status = WriteSector(0, 0, 1, buf);
            Console.Write("Status of write: ");
            Console.WriteNum(status);
            Console.PutChar('\n');
        }

        /// <summary>
        /// Contains test code for ATA
        /// </summary>
        public static void Test()
        {
            Console.WriteLine("Reading 3 bytes of sector on IDE_PRIMARY_MASTER");

            byte[] buf = new byte[512];
            int status;

            status = ReadSector(0, 0, 1, buf);
            Console.Write("status of read: ");
            Console.WriteNum(status);
            Console.PutChar('\n');

            Console.Write("Value: ");
            Console.WriteNum(buf[0]);
            Console.PutChar(' ');
            Console.WriteNum(buf[1]);
            Console.PutChar(' ');
            Console.WriteNum(buf[2]);
            Console.PutChar('\n');

            Console.WriteLine("Reading 3 bytes of sector on IDE_SECONDARY_MASTER");

            status = ReadSector(2, 0, 1, buf);
            Console.Write("status of read: ");
            Console.WriteNum(status);
            Console.PutChar('\n');

            Console.Write("Value: ");
            Console.WriteNum(buf[0]);
            Console.PutChar(' ');
            Console.WriteNum(buf[1]);
            Console.PutChar(' ');
            Console.WriteNum(buf[2]);
            Console.PutChar('\n');
        }
    }
}