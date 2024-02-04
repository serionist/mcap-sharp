using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp.Writer
{
    public class McapWriterOptions
    {
        public bool IgnoreInvalidSchemaID { get; set; } = false;
        public McapCrc32Options Crc32 { get; set; } = new McapCrc32Options();
        
        public AutoSummaryOptions? AutoSummary { get; set; } = new AutoSummaryOptions();
       


    }

    public class McapCrc32Options
    {
        public bool AutoCalculateDataCrc32 { get; set; } = true;
        public bool AutoCalculateSummaryCrc32 { get; set; } = true;
        public bool AutoCalculateAttachmentCrc32 { get; set; } = true;
        public bool AutoCalculateChunkCrc32 { get; set; } = true;
    }

    public class AutoSummaryOptions
    {
        public bool AutoStoreSchemas { get; set; } = true;
        public bool AutoStoreChannels { get; set; } = true;
        public bool AutoStoreStatistics { get; set; } = true;
        public bool AutoCreateSummaryOffsets { get; set; } = true;
        public AutoSummaryIndexOptions? AutoIndexes { get; set; } = new AutoSummaryIndexOptions();

    }

    public class AutoSummaryIndexOptions
    {
        public bool CreateChunkIndexes { get; set; } = true;
        public bool CreateAttachmentIndexes { get; set; } = true;
        public bool CreateMetadataIndex { get; set; } = true;
       
    }
    
}
