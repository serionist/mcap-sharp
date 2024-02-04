using MCAP_csharp.DataTypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Records
{
    public class McapChunkIndex: IMcapSummaryRecord
    {
        public RecordType Type => RecordType.ChunkIndex;

        public McapDateTime MessageStartTime { get; set; }
        public McapDateTime MessageEndTime { get; set; }
        public ulong ChunkStartOffset { get; set; }
        public ulong ChunkLength { get; set; }
        public Dictionary<ushort, ulong> MessageIndexOffsets { get; set; } = new Dictionary<ushort, ulong>();
        public ulong MessageIndexLength { get; set; }
        public McapChunkCompression Compression { get; set; }
        public ulong CompressedSize { get; set; }
        public ulong UncompressedSize { get; set; }

    }
}
