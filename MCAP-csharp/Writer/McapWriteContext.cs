using System;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Text;
using MCAP_csharp.Exceptions;
using MCAP_csharp.Records;

namespace MCAP_csharp.Writer
{
    internal class McapWriteContext
    {

        internal bool DataSectionFinished = false;
        internal readonly Dictionary<ushort, McapSchema> Schemas = new Dictionary<ushort, McapSchema>();
        internal readonly Dictionary<ushort, McapChannel> Channels = new Dictionary<ushort, McapChannel>();

        internal ulong SummaryStart = 0;
        internal ulong SummaryOffsetStart = 0;

        // auto summary records
        internal readonly List<McapChunkIndex> AutoIndexes_ChunkIndexes = new List<McapChunkIndex>();
        internal readonly List<McapAttachmentIndex> AutoIndexes_AttachmentIndexes = new List<McapAttachmentIndex>();
        internal readonly List<McapMetadataIndex> AutoIndexes_MetadataIndexes = new List<McapMetadataIndex>();

        // statistics info
        internal ulong Stats_MessageCount = 0;
        internal uint Stats_AttachmentCount = 0;
        internal uint Stats_MetadataCount = 0;
        internal uint Stats_ChunkCount = 0;
        internal ulong Stats_MessageStartTime = ulong.MaxValue;
        internal ulong Stats_MessageEndTime = 0;
        internal Dictionary<ushort, ulong> Stats_ChannelMessageCounts = new Dictionary<ushort, ulong>();

        // summary 
        internal Dictionary<byte, McapSummaryOffset> SummaryOffsets = new Dictionary<byte, McapSummaryOffset>();
        internal RecordType? Summary_LastOpCode = null;
        internal List<RecordType> Summary_FinishedOpCodes = new List<RecordType>();


    }
}
