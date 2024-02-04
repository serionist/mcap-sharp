using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Records
{
    public class McapMetadataIndex: IMcapSummaryRecord
    {
        public RecordType Type => RecordType.MetadataIndex;
        public ulong Offset { get; set; }
        public ulong Length { get; set; }
        public string Name { get; set; } = "";
    }
}
