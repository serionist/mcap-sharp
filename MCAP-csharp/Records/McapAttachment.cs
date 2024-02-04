using System;
using System.Collections.Generic;
using System.Text;
using MCAP_csharp.DataTypes;

namespace MCAP_csharp.Records
{
    public class McapAttachment: IMcapDataRecord
    {
        public RecordType Type => RecordType.Attachment;
        

        public McapDateTime LogTime { get; set; }
        public McapDateTime CreateTime { get; set; }
        public string Name { get; set; } = "";
        public string MediaType { get; set; } = "";
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public uint Crc { get; set; }
    }
}
