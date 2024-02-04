using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Records
{
    public class McapSummaryOffset: IMcapSummaryRecord
    {
        public RecordType Type => RecordType.SummaryOffset;
        public byte GroupOpCode { get; set; }
        public ulong GroupStart { get; set; }
        public ulong GroupLength { get; set; }
    }
}
