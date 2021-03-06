﻿using Sharpen.FileSystem.Cookie;
using Sharpen.Lib;
using Sharpen.Mem;
using Sharpen.Utilities;

namespace Sharpen.FileSystem.Filesystems
{
    /**
     * 
     * TODO: Make use of fat cache for finding next free cluster, change values in cache when updated
     * TODO: LFN (Long file names)
     * TODO: Cleanup
     * 
     */
    public unsafe class Fat16: IFilesystem
    {
        private const int FirstPartitonEntry = 0x1BE;

        private const int ENTRYACTIVE = 0;
        private const int ENTRYBEGINHEAD = 0x01;
        private const int ENTRYBEGINCYLSEC = 0x02;
        private const int ENTRYTYPE = 0x04;
        private const int ENTRYENDHEAD = 0x05;
        private const int ENTRYENDCYLSEC = 0x06;
        private const int ENTRYNUMSECTORSBETWEEN = 0x08;
        private const int ENTRYNUMSECTORS = 0x0C;

        private const int FAT_FREE = 0x00;
        private const int FAT_EOF = 0xFFF8;

        private Node _Device;
        private int m_bytespersector;
        private int m_beginLBA;
        private int m_clusterBeginLBA;
        private int m_beginDataLBA;


        private Fat16BPB* m_bpb;
        private FatDirEntry* m_dirEntries;
        private uint m_numDirEntries;
        private uint m_fatSize;

        private uint m_sectorOffset;
        private byte* m_fatTable;
        private int m_fatClusterSize;

        private const byte LFN = 0x0F;

        private const int ATTRIB_READONLY = 0x1;
        private const int ATTRIB_HIDDEN = 0x2;
        private const int ATTRIB_SYSFILE = 0x4;
        private const int ATTRIB_VOLUMELABEL = 0x8;
        private const int ATTRIB_SUBDIR = 0x10;
        private const int ATTRIB_ARCHIVE = 0x20;


        #region Initialization

        public static void Register()
        {
            Disk.RegisterFilesystem(new Fat16(), "fat16b");
        }

        /// <summary>
        /// Init and mount FAT on device
        /// </summary>
        /// <param name="deviceNode">Device node</param>
        /// <param name="name">Name</param>
        public unsafe Node Init(Node deviceNode)
        {


            Fat16 fat = new Fat16();

            return fat.InitDevice(deviceNode);
        }

        public unsafe Node InitDevice(Node deviceNode)
        {
            _Device = deviceNode;

            initFAT();



            /**
             * Create and add mountpoint
             */
            Node node = new Node();
            node.ReadDir = readDirImpl;
            node.FindDir = findDirImpl;
            node.Truncate = truncateImpl;
            node.Flags = NodeFlags.DIRECTORY;

            /**
             * Root cookie
             */
            Fat16Cookie rootCookie = new Fat16Cookie();
            rootCookie.Cluster = 0xFFFFFFFF;
            rootCookie.FAT16 = this;

            node.Cookie = rootCookie;
            
            return node;
        }
        
        /// <summary>
        /// Intializes FAT device on device node
        /// </summary>
        /// <param name="dev">Device</param>
        private unsafe void initFAT()
        {
            m_beginLBA = 0;


            byte[] bootSector = new byte[512];
            _Device.Read(_Device, (uint)m_beginLBA, 512, bootSector);
            m_bpb = (Fat16BPB*)Util.ObjectToVoidPtr(bootSector);
            
            int fatRegionSize = m_bpb->NumFats * m_bpb->SectorsPerFat16;
            int rootDirSize = (512 * 32) / m_bpb->BytesPerSector;
            long dataRegionSize = m_bpb->LargeAmountOfSectors - (m_bpb->ReservedSectors + fatRegionSize + rootDirSize);

            m_fatSize = (uint)dataRegionSize / m_bpb->SectorsPerCluster;

            // Cache fat table table
            uint size = (uint)fatRegionSize * 512;

            m_fatTable = (byte*)Heap.Alloc((int)size);
            m_fatClusterSize = fatRegionSize * 256;

            var beginFatTable = m_beginLBA + m_bpb->ReservedSectors;
            
            _Device.Read(_Device, (uint)beginFatTable, (uint)fatRegionSize * 512, Util.PtrToArray(m_fatTable));

            parseBoot();
        }

        /// <summary>
        /// Parse direntries
        /// </summary>
        private unsafe void parseBoot()
        {

            /**
             * Calculate first data start LBA
             */
            m_clusterBeginLBA = m_beginLBA + m_bpb->ReservedSectors + (m_bpb->NumFats * (int)m_bpb->SectorsPerFat16);

            // Fetch root directory from memory
            byte[] buffer = new byte[m_bpb->NumDirEntries * sizeof(FatDirEntry)];

            m_dirEntries = (FatDirEntry*)Util.ObjectToVoidPtr(buffer);


            uint sectorSize = (uint)m_bpb->NumDirEntries / 16;
            
            // Do we have a spare sector?
            if (sectorSize * 16 != m_bpb->NumDirEntries)
                sectorSize++;

            _Device.Read(_Device, (uint)(m_clusterBeginLBA), sectorSize * 512, buffer);

            m_numDirEntries = m_bpb->NumDirEntries;
            m_beginDataLBA = m_clusterBeginLBA + ((m_bpb->NumDirEntries * 32) / m_bpb->BytesPerSector);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Create FS node
        /// </summary>
        /// <param name="dirEntry">Direntry</param>
        /// <param name="cluster">Directory cluster</param>
        /// <param name="num">Direnty number</param>
        /// <returns></returns>
        public Node CreateNode(FatDirEntry* dirEntry, uint cluster, uint num)
        {

            Node node = new Node();
            node.Size = dirEntry->Size;

            Fat16Cookie cookie = new Fat16Cookie();
            cookie.DirEntry = dirEntry;
            cookie.Cluster = cluster;
            cookie.Num = num;
            cookie.FAT16 = this;

            node.Cookie = cookie;

            /**
             * Is it a directory?
             */
            if ((dirEntry->Attribs & ATTRIB_SUBDIR) == ATTRIB_SUBDIR)
            {
                node.ReadDir = readDirImpl;
                node.FindDir = findDirImpl;
                node.Flags = NodeFlags.DIRECTORY;
            }
            else
            {
                node.Read = readImpl;
                node.Write = writeImpl;
                node.Truncate = truncateImpl;
                node.Flags = NodeFlags.FILE;
            }

            return node;
        }

        /// <summary>
        /// Cluster to LBA
        /// </summary>
        /// <param name="cluster">Cluster number</param>
        /// <returns></returns>
        public uint Data_clust_to_lba(uint cluster)
        {
            return (uint)(m_beginDataLBA + (cluster - 2) * m_bpb->SectorsPerCluster);
        }

        /// <summary>
        /// Find next cluster in file
        /// </summary>
        /// <param name="cluster">Cluster number</param>
        /// <returns></returns>
        public unsafe ushort FindNextCluster(uint cluster)
        {
            ushort nextClusterCached = *(ushort*)(m_fatTable + (cluster * 2));

            if (nextClusterCached >= FAT_EOF)
            {
                return 0xFFFF;
            }
            
            return nextClusterCached;
        }

        /// <summary>
        /// Calculate offset in fat table
        /// </summary>
        /// <param name="cluster"></param>
        /// <returns></returns>
        public unsafe uint CalculateFatOffset(uint cluster)
        {

            int beginFat = m_beginLBA + m_bpb->ReservedSectors;
            uint clusters = (cluster / 256);
            uint adr = (uint)((beginFat * 512) + clusters * 512);
            uint offset = (cluster * 2) - (clusters * 512);

            return adr + offset;
        }

        /// <summary>
        /// Change cluster value in FAT
        /// </summary>
        /// <param name="cluster">Cluster number</param>
        /// <param name="value">Fat value</param>
        private unsafe void changeClusterValue(uint cluster, ushort value)
        {

            // update cache item
            *(ushort*)(m_fatTable + (cluster * 2)) = value;

            // Update disk
            int beginFat = m_beginLBA + m_bpb->ReservedSectors;
            uint clusters = (cluster / 256);
            uint adr = (uint)(beginFat + clusters);
            uint offset = (cluster * 2) - (clusters * 512);


            byte[] fatBuffer = new byte[512];
            _Device.Read(_Device, adr, 512, fatBuffer);

            byte* ptr = (byte*)Util.ObjectToVoidPtr(fatBuffer);
            ushort* pointer = (ushort*)(ptr + offset);
            
            *pointer = value;
            

            _Device.Write(_Device, adr, 512, fatBuffer);
            
            Heap.Free(ptr);
        }

        /// <summary>
        /// Find last cluster for file in FAT
        /// </summary>
        /// <returns>Last cluster of file in FAT</returns>
        private ushort findLastCluster(ushort cluster)
        {

            ushort lastValue = cluster;
            ushort lastResult = cluster;
            while (lastResult != 0xFFFF)
            {
                lastValue = lastResult;
                lastResult = FindNextCluster(lastResult);
            }

            return lastValue;
        }

        /// <summary>
        /// Find last cluster for file in FAT
        /// </summary>
        /// <returns>Last cluster of file in FAT</returns>
        private ushort findLastCluster(ushort cluster, uint offset)
        {

            ushort lastValue = cluster;
            ushort lastResult = cluster;

            int count = 0;
            while (lastResult != 0xFFFF)
            {
                lastResult = FindNextCluster(lastResult);
                count++;
            }

            lastResult = cluster;

            int target = count - (int)offset;

            while (target > 0)
            {
                target--;
                lastValue = lastResult;
                lastResult = FindNextCluster(lastResult);

            }

            return lastValue;
        }

        /// <summary>
        /// Find next free cluster in FAT
        /// </summary>
        /// <returns></returns>
        private ushort findNextFreeCluster()
        {

            for(ushort i = 0; i < m_fatClusterSize; i++)
            {

                ushort cluster = *(ushort *)(m_fatTable + (i * 2));

                if (cluster == 0x0000)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// Set file size in Direntries
        /// </summary>
        /// <param name="cluster">Start cluster</param>
        /// <param name="num">Direnty location</param>
        /// <param name="size">Size</param>
        private void SetFileSize(uint cluster, uint num, uint size)
        {

            uint offset = num * (uint)sizeof(FatDirEntry);

            uint offsetSector = offset / 512;
            offset -= offsetSector * 512;

            // Read dir entry part
            uint realOffset = 0;
            if (cluster == 0xFFFFFFFF)
                realOffset = (uint)(m_clusterBeginLBA + offsetSector);
            else
                realOffset = Data_clust_to_lba(cluster) + offsetSector;

            byte[] buf = new byte[512];
            _Device.Read(_Device, realOffset, 512, buf);


            byte* bufPtr = (byte*)Util.ObjectToVoidPtr(buf);
            FatDirEntry* entry = (FatDirEntry*)(bufPtr + offset);

            entry->Size = size;
            
            _Device.Write(_Device, realOffset, 512, buf);

            // Update dir entry if needed
            if (cluster == 0xFFFFFFFF)
            {
                m_dirEntries[num].Size = size;
            }
        }

        /// <summary>
        /// Find node in directory
        /// </summary>
        /// <param name="cluster">Cluster number</param>
        /// <param name="testFor">Compare string</param>
        /// <returns></returns>
        public Node FindFileInDirectory(uint cluster, char* testFor)
        {

            SubDirectory dir = readDirectory(cluster);

            for (int i = 0; i < dir.Length; i++)
            {

                FatDirEntry entry = dir.DirEntries[i];

                if (entry.Name[0] == 0 || entry.Name[0] == 0xE5 || entry.Attribs == 0xF || (entry.Attribs & 0x08) > 0)
                    continue;
                
                if (Memory.Compare(testFor, entry.Name, 11))
                {
                    FatDirEntry* entr = (FatDirEntry*)Heap.Alloc(sizeof(FatDirEntry));
                    Memory.Memcpy(entr, dir.DirEntries + i, sizeof(FatDirEntry));
                    return CreateNode(entr, cluster, (uint)i);
                }
            }

            return null;
        }
        
        /// <summary>
        /// Read directory
        /// </summary>
        /// <param name="cluster"></param>
        /// <returns></returns>
        public SubDirectory readDirectory(uint cluster)
        {

            SubDirectory outDir = new SubDirectory();

            if (cluster == 0xFFFFFFFF)
            {
                outDir.Length = m_numDirEntries;
                outDir.DirEntries = m_dirEntries;
            }
            else
            {
                byte[] buffer = new byte[m_bpb->NumDirEntries * sizeof(FatDirEntry)];
                
                // To-do why does this read that mutch?
                readFile(cluster, 0, (uint)(m_bpb->NumDirEntries * sizeof(FatDirEntry)), buffer);

                FatDirEntry* entries = (FatDirEntry*)Heap.Alloc(m_bpb->NumDirEntries * sizeof(FatDirEntry));

                FatDirEntry* curBufPtr = (FatDirEntry*)Util.ObjectToVoidPtr(buffer);

                int length = 0;
                for (int i = 0; i < m_bpb->NumDirEntries; i++)
                {
                    entries[i] = curBufPtr[i];

                    if (curBufPtr[i].Name[0] == 0x00)
                    {
                        break;

                    }

                    length++;
                }

                outDir.DirEntries = entries;
                outDir.Length = (uint)length;
            }

            return outDir;
        }

        #endregion

        #region Read/Write

        /// <summary>
        /// Write file (NOTE: This will stop when file is ending, not resizing file yet)
        /// </summary>
        /// <param name="startCluster">Start cluster</param>
        /// <param name="offset">Start offset</param>
        /// <param name="size">Size in bytes</param>
        /// <param name="buffer">Input buffer</param>
        /// <returns>Bytes written</returns>
        private uint writeFile(uint startCluster, uint offset, uint size, byte[] buffer)
        {
            // Calculate starting cluster
            uint dataPerCluster = m_bpb->SectorsPerCluster;
            uint bytesPerCluster = dataPerCluster * 512;

            uint sectorsOffset = (uint)((int)offset / 512);

            uint clusterOffset = sectorsOffset / dataPerCluster;

            if (clusterOffset > 0)
            {
                for (int i = 0; i < clusterOffset; i++)
                {
                    startCluster = FindNextCluster(startCluster);

                    if (startCluster == 0xFFFF)
                        return 0;
                }
            }

            uint StartOffset = offset - (sectorsOffset * 512);
            sectorsOffset = sectorsOffset - (clusterOffset * m_bpb->SectorsPerCluster);

            // Read starting cluster
            byte[] buf = new byte[512];
            _Device.Read(_Device, Data_clust_to_lba(startCluster), 512, buf);

            // Calculate size in sectors
            uint sizeInSectors = size / 512;
            if (sizeInSectors == 0)
                sizeInSectors++;

            uint offsetInCluster = sectorsOffset;
            uint offsetInSector = StartOffset;
            uint currentCluster = startCluster;
            uint currentOffset = 0;
            int sizeLeft = (int)size;



            for (int i = 0; i < sizeInSectors; i++)
            {
                if (offsetInCluster == m_bpb->SectorsPerCluster)
                {
                    currentCluster = FindNextCluster(currentCluster);

                    if (currentCluster == 0xFFFF)
                        return currentOffset;

                    offsetInCluster = 0;
                }

                int sizeTemp = (sizeLeft > 512) ? 512 : sizeLeft;
                int sizeLeftinSector = 512;
                sizeLeftinSector -= (int)offsetInSector;
                if (sizeLeft > sizeLeftinSector)
                {
                    sizeTemp = sizeLeftinSector;
                    sizeInSectors++;
                }

                _Device.Read(_Device, Data_clust_to_lba(currentCluster) + offsetInCluster, 512, buf);
                Memory.Memcpy((byte*)Util.ObjectToVoidPtr(buf) + offsetInSector, (byte*)Util.ObjectToVoidPtr(buffer) + currentOffset, sizeTemp);

                _Device.Write(_Device, Data_clust_to_lba(currentCluster) + offsetInCluster, 512, buf);

                currentOffset += (uint)sizeTemp;
                sizeLeft -= sizeTemp;
                offsetInCluster++;
                offsetInSector = 0;
            }


            Heap.Free(buf);
            
            return currentOffset;
        }

        /// <summary>
        /// Read file
        /// </summary>
        /// <param name="startCluster">Start cluster</param>
        /// <param name="offset">Start offset</param>
        /// <param name="size">Size in bytes</param>
        /// <param name="buffer">Input buffer</param>
        /// <returns>Bytes read</returns>
        private uint readFile(uint startCluster, uint offset, uint size, byte[] buffer)
        {

            // Calculate starting cluster
            uint dataPerCluster = m_bpb->SectorsPerCluster;
            uint sectorsOffset = (uint)((int)offset / 512);
            uint bytesPerCluster = dataPerCluster * 512;

            uint clusterOffset = sectorsOffset / dataPerCluster;

            if (clusterOffset > 0)
            {
                for (int i = 0; i < clusterOffset; i++)
                {
                    startCluster = FindNextCluster(startCluster);

                    if (startCluster == 0xFFFF)
                        return 0;
                }
            }

            uint StartOffset = offset - (sectorsOffset * 512);
            sectorsOffset = sectorsOffset - (clusterOffset * m_bpb->SectorsPerCluster);

            // Read starting cluster
            byte[] buf = new byte[bytesPerCluster];
            _Device.Read(_Device, Data_clust_to_lba(startCluster), bytesPerCluster, buf);
            
            uint offsetinCluster = (sectorsOffset * 512) + StartOffset;
            uint currentCluster = startCluster;
            uint currentOffset = 0;
            uint sizeLeft = size;

            while (sizeLeft > 0)
            {
                // Get size to copy for this sector
                uint sizeTemp = bytesPerCluster;
                if (sizeLeft < bytesPerCluster)
                    sizeTemp = sizeLeft;

                // Copy the read bytes
                Memory.Memcpy((byte*)Util.ObjectToVoidPtr(buffer) + currentOffset, (byte*)Util.ObjectToVoidPtr(buf) + offsetinCluster, (int)sizeTemp);

                // Advance a step
                currentOffset += sizeTemp;
                sizeLeft -= sizeTemp;
                
                // Do we need to read another cluster?
                if (sizeLeft == 0)
                    break;

                currentCluster = FindNextCluster(currentCluster);
                
                // Have we reached the end?
                if (currentCluster == 0xFFFF)
                    break;
;

                _Device.Read(_Device, Data_clust_to_lba(currentCluster), bytesPerCluster, buf);
            }
            

            Heap.Free(buf);

            return size;
        }

        #endregion

        #region Util

        /// <summary>
        /// Resize file
        /// 
        /// @TODO: Allow cluster allocation and removal
        /// 
        /// /// </summary>
        /// <param name="direntry">Current direntry</param>
        /// <param name="cluster">Cluster number for writing</param>
        /// <param name="num">Direntry number in cluster</param>
        /// <param name="size">File size</param>
        /// <returns>Changed size (=0 when error)</returns>
        private uint ResizeFile(FatDirEntry* direntry, uint cluster, uint num, uint size)
        {
            uint realsize = size;

            int bytesPerCluster = m_bpb->SectorsPerCluster * m_bpb->BytesPerSector;

            uint readSizeNew = size - 1;
            uint readSizeOld = direntry->Size - 1;

            /**
             * Calculate sectors and clusters
             */
            uint sectorsNew = (uint)Math.Ceil((double)readSizeNew / (double)m_bpb->BytesPerSector);
            uint clustersNew = (uint)Math.Ceil((double)sectorsNew / (double)m_bpb->SectorsPerCluster);
            if (readSizeNew == bytesPerCluster)
                clustersNew++;

            uint sectorsOld = (uint)Math.Ceil((double)(readSizeOld) / (double)m_bpb->BytesPerSector);
            uint clustersOld = (uint)Math.Ceil((double)sectorsOld / (double)m_bpb->SectorsPerCluster);
            if (readSizeOld == bytesPerCluster)
                clustersOld++;

            /**
             * Calculate difference
             */
            int clusterDiff = (int)clustersNew - (int)clustersOld;

            /**
             * @TODO: Test this! (lightly tested, looks like is that it is working)
             */
            if (clusterDiff != 0)
            {
                /**
                 * Increase or decrease
                 */ 
                if(clusterDiff < 0)
                {

                    clusterDiff = Math.Abs(clusterDiff);

                    ushort currentLastCluster = findLastCluster((ushort)cluster);
                    ushort newLastCluster = findLastCluster((ushort)cluster, 1);

                    /**
                     * Removing clusters
                     */
                    for (int i = 0; i < clusterDiff; i++)
                    {

                        /**
                         * Change cluster table values
                         */
                        changeClusterValue(currentLastCluster, 0x0000);
                        changeClusterValue(newLastCluster, 0xFFFF);

                        currentLastCluster = findLastCluster((ushort)cluster);
                        newLastCluster = findLastCluster((ushort)cluster, 1);
                    }
                }
                else
                {
                    
                    ushort currentCluster = findLastCluster((ushort)cluster);
                    

                    byte[] buf = new byte[m_bpb->BytesPerSector];
                    Memory.Memclear(Util.ObjectToVoidPtr(buf), m_bpb->BytesPerSector);

                    /**
                     * Allocating clusters
                     */
                    for (int i = 0; i < clusterDiff; i++)
                    {

                        ushort freeCluster = findNextFreeCluster();

                        /**
                         * Change cluster table values
                         */
                        changeClusterValue(currentCluster, freeCluster);
                        changeClusterValue(freeCluster, 0xFFFF);

                        currentCluster = freeCluster;

                        /**
                         * Free cluster
                         */
                        uint toFreeLBA = Data_clust_to_lba(currentCluster);
                        
                        for(uint x = 0; x < m_bpb->SectorsPerCluster; x++)
                            _Device.Write(_Device, toFreeLBA + x, m_bpb->BytesPerSector, buf);

                    }

                    Heap.Free(buf);
                }
            }

            /**
             * CLEAR EMPTY SPACE
             * 
             * Calculate startpoint and offsets
             */
            uint startPoint = (uint)(size > direntry->Size ? direntry->Size : size);
            uint offsetSector = startPoint / 512;
            uint offset = startPoint - (offsetSector * 512);

            /**
             * Get starting position on FS
             */
            uint lba = Data_clust_to_lba(direntry->ClusterNumberLo);

            byte[] writeSec = new byte[m_bpb->BytesPerSector];
            byte[] readSec = new byte[m_bpb->BytesPerSector];

            /**
             * Free cluster
             */
            for (uint i = offsetSector; i < m_bpb->SectorsPerCluster; i++)
            {
                /**
                 * Do we have an offset to use?
                 */
                if (offset != 0)
                {
                    _Device.Read(_Device, lba + i, 512, readSec);
                    Memory.Memcpy(Util.ObjectToVoidPtr(writeSec), Util.ObjectToVoidPtr(readSec), (int)offset);
                    Memory.Memclear((byte*)Util.ObjectToVoidPtr(writeSec) + offset, (int)(512 - offset));
                }
                else
                {
                    Memory.Memclear(Util.ObjectToVoidPtr(writeSec), 512);
                }

                offset = 0;
                _Device.Write(_Device, lba + i, 512, writeSec);
            }

            /**
             * Free objects
             */
            Heap.Free(Util.ObjectToVoidPtr(writeSec));
            Heap.Free(Util.ObjectToVoidPtr(readSec));
            
            /**
             * Finally update node!
             */
            SetFileSize(cluster, num, realsize);

            direntry->Size = realsize;

            return realsize;
        }

        #endregion

        #region Node implementations

        /// <summary>
        /// Find directory implementation
        /// </summary>
        /// <param name="node">FS Node</param>
        /// <param name="name">Filename</param>
        /// <returns></returns>
        private static Node findDirImpl(Node node, string name)
        {
            Fat16Cookie cookie = (Fat16Cookie)node.Cookie;
            Fat16 fat16 = cookie.FAT16; 
            
            /**
             * Calculate lengths (and check if no LFN)
             */
            int length = name.Length;
            if (length > 12)
                return null;

            int dot = name.IndexOf('.');
            if (dot > 8)
                return null;

            /**
             * Prepare test memory block
             */
            char* testFor = (char*)Heap.Alloc(11);
            Memory.Memset(testFor, ' ', 11);
            
            int min = (dot == -1) ? Math.Min(length, 8) : Math.Min(dot, 8);
            int i = 0;
            for (; i < min; i++)
            {
                testFor[i] = String.ToUpper(name[i]);
            }

            if (dot != -1)
            {
                int lengthExt = length - dot - 1;
                min = Math.Min(3, lengthExt);

                i++;
                for (int j = 0; j < min; j++)
                {
                    testFor[j + 8] = String.ToUpper(name[i + j]);
                }
            }
            
            /**
             * Find cluster number
             */
            uint cluster = 0xFFFFFFFF;
            if (cookie.DirEntry != null)
            {
                FatDirEntry* entry = cookie.DirEntry;
                cluster = entry->ClusterNumberLo;
            }
            /**
             * Find file in cluster (directory)
             */
            Node nd = fat16.FindFileInDirectory(cluster, testFor);

            Heap.Free(testFor);

            return nd;
        }

        private static void GetName(SubDirectory dir, int i, char *outName)
        {
            FatDirEntry entry = dir.DirEntries[i];

            /**
             * Calculate length and offsets
             */
            string nameStr = Util.CharPtrToString(entry.Name);
            int fnLength = nameStr.IndexOf(' ');

            if (fnLength > 8 || fnLength == -1)
                fnLength = 8;

            int offset = 0;
            for (int z = 0; z < fnLength; z++)
                outName[offset++] = entry.Name[z];


            // Is it a file?
            if ((dir.DirEntries[i].Attribs & ATTRIB_SUBDIR) == 0)
            {
                nameStr = Util.CharPtrToString(entry.Name + 8);
                int extLength = nameStr.IndexOf(' ');
                if (extLength == -1)
                    extLength = 3;

                if (extLength != 0)
                {
                    outName[offset++] = '.';


                    for (int z = 0; z < extLength; z++)
                        outName[offset++] = entry.Name[z + 8];
                }
            }

            outName[offset] = '\0';

            for (int z = 0; z < offset; z++)
                outName[z] = String.ToLower(outName[z]);
        }

        /// <summary>
        /// Filesystem read directory implementation
        /// </summary>
        /// <param name="node"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private static DirEntry* readDirImpl(Node node, uint index)
        {
            int j = 0;

            /**
             * Find cluster number if not root directory
             */
            uint cluster = 0xFFFFFFFF;

            Fat16Cookie cookie = (Fat16Cookie)node.Cookie;
            Fat16 fat = cookie.FAT16;
            if (cookie.DirEntry != null)
            {
                FatDirEntry* entry = cookie.DirEntry;
                cluster = entry->ClusterNumberLo;
            }

            /**
             * Read directory entries
             */
            SubDirectory dir = fat.readDirectory(cluster);

            for (int i = 0; i < dir.Length; i++)
            {
                FatDirEntry entry = dir.DirEntries[i];

                /**
                 * Correct attributes?
                 */
                if (entry.Name[0] == 0 || entry.Name[0] == (char)0xE5 || entry.Attribs == 0xF || (entry.Attribs & 0x08) > 0)
                    continue;

                /**
                 * Do we need to search further?
                 */
                if (j >= index)
                {
                    DirEntry* outDir = (DirEntry*)Heap.Alloc(sizeof(DirEntry));
                    outDir->Reclen = (ushort)sizeof(DirEntry);

                    GetName(dir, i, outDir->Name);


                    /**
                     * Directory or file?
                     */
                    if ((dir.DirEntries[i].Attribs & ATTRIB_SUBDIR) == 0)
                    {

                        outDir->Type = (byte)DT_Type.DT_REG;
                    }
                    else
                    {
                        outDir->Type = (byte)DT_Type.DT_DIR;
                    }


                    return outDir;
                }

                j++;
            }

            return null;
        }

        /// <summary>
        /// Filesystem read implementation
        /// </summary>
        /// <param name="node"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        private static uint readImpl(Node node, uint offset, uint size, byte[] buffer)
        {
            /**
             * Get directory entry from cookie "cache"
             */
            Fat16Cookie cookie = (Fat16Cookie)node.Cookie;
            Fat16 fat = cookie.FAT16;
            FatDirEntry* entry = cookie.DirEntry;

            /*
             * If the offset is behind the size, stop here
             */
            if (offset > entry->Size)
                return 0;

            /*
             * If bytes to read is bigger than the file size, set the size to the file size minus offset
             */
            if (offset + size > entry->Size)
                size = entry->Size - offset;

            //Util.PrintStackTrace(6);
            
            uint read = fat.readFile(entry->ClusterNumberLo, offset, size, buffer);

            return read;
        }

        /// <summary>
        /// Filesystem write implementation
        /// </summary>
        /// <param name="node"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        private static uint writeImpl(Node node, uint offset, uint size, byte[] buffer)
        {
            /**
             * Get directory entry from cookie "cache"
             */
            Fat16Cookie cookie = (Fat16Cookie)node.Cookie;
            Fat16 fat = cookie.FAT16;
            FatDirEntry* entry = cookie.DirEntry;
            
            uint totalSize = size + offset;

            /**
             * Do we need to resize the file?
             */
            if (entry->Size < totalSize)
            {
                if (fat.ResizeFile(entry, cookie.Cluster, cookie.Num, totalSize) == 0)
                    return 0;
            }
            
            /**
             * Handle file writing
             */
            return fat.writeFile(entry->ClusterNumberLo, offset, size, buffer);
        }
        
        /// <summary>
        /// Filesystem truncate implementation
        /// </summary>
        /// <param name="node"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        private static uint truncateImpl(Node node, uint size)
        {
            /**
             * Get directory entry from cookie "cache"
             */
            Fat16Cookie cookie = (Fat16Cookie)node.Cookie;
            FatDirEntry* entry = cookie.DirEntry;
            Fat16 fat = cookie.FAT16;

            if (entry == null)
                return 0;

            /**
             * Empty file not supported
             */
            if (size == 0)
                return 0;
            
            /**
             * Resize file
             */
            return fat.ResizeFile(entry, cookie.Cluster, cookie.Num, size);
        }


        #endregion
        
    }
}
