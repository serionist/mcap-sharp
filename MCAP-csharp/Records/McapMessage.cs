using MCAP_csharp.DataTypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Records
{
    public class McapMessage : IMcapRecord, IMcapDataRecord, IMcapChunkContentRecord
    {
        public RecordType Type => RecordType.Message;
        public ushort ChannelId { get; set; }
        public uint Sequence { get; set; }
        public McapDateTime LogTime { get; set; }
        public McapDateTime PublishTime { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }
}
