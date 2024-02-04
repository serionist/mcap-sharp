using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Records
{
    public class McapMetadata: IMcapDataRecord
    {
        public RecordType Type => RecordType.Metadata;
        public string Name { get; set; } = "";
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
