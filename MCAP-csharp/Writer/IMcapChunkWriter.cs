using MCAP_csharp.Records;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace MCAP_csharp.Writer
{
    public interface IMcapChunkWriter: IDisposable
    {
        public ulong UncompressedRecordsSize { get; }
        void WriteChunkRecord(IMcapChunkContentRecord record);
        void WriteChunk();
    }
}
