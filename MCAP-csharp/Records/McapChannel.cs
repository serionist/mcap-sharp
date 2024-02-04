using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MCAP_csharp.Records
{
    public class McapChannel : IMcapRecord, IMcapDataRecord, IMcapSummaryRecord, IMcapChunkContentRecord, IEquatable<McapChannel>
    {
        public RecordType Type => RecordType.Channel;
        public ushort Id { get; set; }
        public ushort SchemaId { get; set; }
        public string Topic { get; set; } = "";
        public string MessageEncoding { get; set; } = "";
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();


        public bool Equals(McapChannel? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id && SchemaId == other.SchemaId && Topic == other.Topic && MessageEncoding == other.MessageEncoding && Metadata.OrderBy(e => e.Key)
                .SequenceEqual((other?.Metadata ?? new Dictionary<string, string>()).OrderBy(e => e.Key)) == true;
            ;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((McapChannel) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, SchemaId, Topic, MessageEncoding, Metadata);
        }
    }
}
