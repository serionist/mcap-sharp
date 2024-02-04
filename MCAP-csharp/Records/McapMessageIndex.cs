using MCAP_csharp.DataTypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Records
{
    public class McapMessageIndex: IMcapDataRecord
    {
        public RecordType Type => RecordType.MessageIndex;
        public ushort ChannelId { get; set; }
        public Tuple<McapDateTime, ulong>[] Records { get; set; } = Array.Empty<Tuple<McapDateTime, ulong>>();
    }
}
