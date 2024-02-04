using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Records
{
    public class McapUnknownRecord: IMcapSummaryRecord, IMcapDataRecord, IMcapChunkContentRecord
    {
        public RecordType Type => RecordType.Unknown;
        public byte OpCode { get; set; }
    }
}
