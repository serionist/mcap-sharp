using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Records
{
    public class McapHeader: IMcapRecord
    {
        public RecordType Type => RecordType.Header;
        public string Profile { get; set; }
        public string Library { get; set; }
    }
}
