using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Records
{
    public class McapFooter : IMcapRecord
    {
        public RecordType Type => RecordType.Footer;
        public ulong SummaryStart { get; set; }
        public ulong SummaryOffsetStart { get; set; }
        public uint SummaryCrc { get; set; }
    }
}
