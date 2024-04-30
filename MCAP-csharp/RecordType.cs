using System;
using System.Collections.Generic;
using System.Text;

namespace MCAP_csharp
{
    public enum RecordType: byte
    {
        Header = 0x01,
        Footer = 0x02,
        Schema = 0x03,
        Channel = 0x04,
        Message = 0x05,
        Chunk = 0x06,
        MessageIndex = 0x07,
        ChunkIndex = 0x08,
        Attachment = 0x09,
        Metadata = 0x0C,
        DataEnd = 0x0F,
        AttachmentIndex = 0x0A,
        MetadataIndex = 0x0D,
        Statistics = 0x0B,
        SummaryOffset = 0x0E,
        Unknown
    }
}
