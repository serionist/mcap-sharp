using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MCAP_csharp.Records
{
    public class McapSchema : IMcapRecord, IMcapDataRecord, IMcapSummaryRecord, IMcapChunkContentRecord, IEquatable<McapSchema>
    {
        public RecordType Type => RecordType.Schema;
        public ushort Id { get; set; }
        public string Name { get; set; } = "";
        public string Encoding { get; set; } = "";
        public byte[] Data { get; set; } = Array.Empty<byte>();


          public bool Equals(McapSchema? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id && Name == other.Name && Encoding == other.Encoding && Data?.SequenceEqual(other?.Data ?? Array.Empty<byte>()) == true;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((McapSchema) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Name, Encoding, Data);
        }
    }
}
