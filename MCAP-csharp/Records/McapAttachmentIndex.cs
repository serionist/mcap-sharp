using System;
using System.Collections.Generic;
using System.Text;
using MCAP_csharp.DataTypes;

namespace MCAP_csharp.Records
{
    public class McapAttachmentIndex: IMcapSummaryRecord
    {
        public RecordType Type => RecordType.AttachmentIndex;
        public ulong Offset { get; set; }
        public ulong Length { get; set; }
        public McapDateTime LogTime { get; set; }
        public McapDateTime CreateTime { get; set; }
        public ulong DataSize { get; set; }
        public string Name { get; set; } = "";
        public string MediaType { get; set; } = "";
    }
}
