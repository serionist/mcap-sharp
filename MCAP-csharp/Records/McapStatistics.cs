using System;
using System.Collections.Generic;
using System.Text;
using MCAP_csharp.DataTypes;

namespace MCAP_csharp.Records
{
    public class McapStatistics: IMcapSummaryRecord
    {
        public RecordType Type => RecordType.Statistics;
        public ulong MessageCount { get; set; }
        public ushort SchemaCount { get; set; }
        public uint ChannelCount { get; set; }
        public uint AttachmentCount { get; set; }
        public uint MetadataCount { get; set; }
        public uint ChunkCount { get; set; }
        public McapDateTime MessageStartTime { get; set; }
        public McapDateTime MessageEndTime { get; set; }
        public Dictionary<ushort, ulong> ChannelMessageCounts { get; set; } = new Dictionary<ushort, ulong>();
    }
}
