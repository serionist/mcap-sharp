using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MCAP_csharp.DataTypes;

namespace MCAP_csharp.Records
{
    public class McapChunk: IMcapRecord, IMcapDataRecord
    {
        public RecordType Type => RecordType.Chunk;
        public McapDateTime MessageStartTime { get; set; }
        public McapDateTime MessageEndTime { get; set; }
        public ulong UncompressedSize { get; set; }
        public uint UncompressedCrc { get; set; }
        public McapChunkCompression Compression { get; set; }
        internal ulong Read_RecordsByteLength { get; set; }
        internal long Read_RecordsBytesStart { get; set; }
        internal MemoryStream? Write_CompressedStream { get; set; }
        


    }

    public enum McapChunkCompression
    {
        none,
        zstd,
        lz4
    }
}
