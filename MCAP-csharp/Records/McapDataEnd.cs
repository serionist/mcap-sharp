using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Records
{
    public class McapDataEnd: IMcapDataRecord
    {
        public RecordType Type => RecordType.DataEnd;
        public uint DataSectionCrc { get; set; }
    }
}
