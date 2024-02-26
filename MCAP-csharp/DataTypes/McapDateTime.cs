using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.DataTypes
{
    public struct McapDateTime
    {
        public McapDateTime(ulong nanoSeconds)
        {
            NanoSeconds = nanoSeconds;
        }

        public McapDateTime(DateTime approximateDateTime)
        {
            NanoSeconds = 0;
            ApproximateDateTime = approximateDateTime;
        }
        public ulong NanoSeconds { get; set; }
        public DateTime? ApproximateDateTime
        {
            get => NanoSeconds == 0 ? (DateTime?)null: new DateTime(1970, 1, 1).AddTicks((long)NanoSeconds / 100);
            set => NanoSeconds = !value.HasValue ? 0: (ulong)value.Value.Subtract(new DateTime(1970, 1, 1)).Ticks * 100;
        }
    }
}
