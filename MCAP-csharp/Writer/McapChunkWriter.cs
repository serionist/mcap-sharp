using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using K4os.Compression.LZ4.Streams;
using MCAP_csharp.DataTypes;
using MCAP_csharp.Exceptions;
using MCAP_csharp.Records;
using ZstdSharp;

namespace MCAP_csharp.Writer
{
    internal class McapChunkWriter: IMcapChunkWriter
    {
        private readonly McapWriteContext _context;
        private readonly McapWriterOptions _options;
        private readonly McapWriter _writer;
        public McapChunkWriter(McapWriteContext context, McapWriterOptions options, McapWriter writer, McapChunkCompression compression, bool storeMessageIndex)
        {
            _context = context;
            _options = options;
            _writer = writer;
            Compression = compression;
            StoreMessageIndex = storeMessageIndex;
            if (options.Crc32?.AutoCalculateChunkCrc32 == true)
                UncompressedCrc = new Crc32();
            BaseStream = ReadWriteHelper._memStreamManager.GetStream();
            switch (Compression)
            {
                case McapChunkCompression.none:
                    _compressedStream = BaseStream;
                    break;
                case McapChunkCompression.zstd:
                    _compressedStream = new CompressionStream(BaseStream);
                    break;
                case McapChunkCompression.lz4:
                    _compressedStream = LZ4Stream.Encode(BaseStream, null, true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }

        private bool _flushed = false;
        internal ulong MessageStartTime = ulong.MaxValue;
        internal ulong MessageEndTime = 0;
        public ulong UncompressedRecordsSize { get; private set; }
        internal readonly Crc32? UncompressedCrc;
        internal readonly McapChunkCompression Compression;
        internal readonly bool StoreMessageIndex;
        internal readonly MemoryStream BaseStream;
        private readonly Stream _compressedStream;

        internal readonly Dictionary<ushort, List<Tuple<McapDateTime, ulong>>> MessageIndex =
            new Dictionary<ushort, List<Tuple<McapDateTime, ulong>>>();
        
        public void WriteChunkRecord(IMcapChunkContentRecord record)
        {
            if (_flushed)
                throw new McapWriteException(
                    $"Cannot write into IMcapChunkWriter after it has been Disposed or 'FinishChunk' has been called");
            if (_context.DataSectionFinished)
                throw new McapWriteException(
                    $"Cannot write into Chunk after MCapDataEnd was written or summary section has been started");

            if (record is McapMessage m)
            {
                MessageStartTime = Math.Min(MessageStartTime, m.LogTime.NanoSeconds);
                MessageEndTime = Math.Max(MessageEndTime, m.LogTime.NanoSeconds);
                if (StoreMessageIndex)
                {
                    var channelIndex = MessageIndex.GetValueOrDefault(m.ChannelId);
                    if (channelIndex == null)
                    {
                        channelIndex = new List<Tuple<McapDateTime, ulong>>();
                        MessageIndex.Add(m.ChannelId, channelIndex);
                    }
                    channelIndex.Add(new Tuple<McapDateTime, ulong>(m.LogTime, UncompressedRecordsSize));
                }
            }
            UncompressedRecordsSize += ReadWriteHelper.writeRecordAtCurrentPos(_compressedStream, record, UncompressedCrc, _options, _context);

        }


        public void WriteChunk()
        {
            if (_flushed)
                return;
            _flushed = true;
            if (_compressedStream != BaseStream)
                _compressedStream.Dispose();
            _writer.FlushChunk(this);
           
        }

        public void Dispose() => WriteChunk();
    }
}
