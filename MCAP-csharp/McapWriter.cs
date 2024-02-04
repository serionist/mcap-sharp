using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MCAP_csharp.DataTypes;
using MCAP_csharp.Exceptions;
using MCAP_csharp.Records;
using MCAP_csharp.Writer;

namespace MCAP_csharp
{
    public class McapWriter: IDisposable
    {
        private readonly Stream _stream;
        private readonly McapWriterOptions _options;
        private readonly Crc32? _dataCrc32;
        private readonly Crc32? _summaryCrc32;
        private readonly McapWriteContext _context;
        private McapWriter(Stream stream, McapWriterOptions options)
        {
            _stream = stream;
            _options = options;
            _context = new McapWriteContext();
            if (options.Crc32.AutoCalculateDataCrc32)
                _dataCrc32 = new Crc32();
            if (options.Crc32.AutoCalculateSummaryCrc32)
                _summaryCrc32 = new Crc32();
        }
        public ulong FilePosition { get; private set; }
        public static McapWriter Create(Stream stream, string profile = "ros2", string library = "mcap-csharp-0.1", McapWriterOptions? opts = null)
        {
            var ret = new McapWriter(stream, opts ?? new McapWriterOptions());
            ret.initialize(profile, library);
            return ret;

        }

        private void initialize(string profile, string library)
        {
            if (!_stream.CanWrite)
                throw new InvalidOperationException("McapWriter requires 'CanWrite' on source stream");
            // write magic bytes
            var magicBytes = writeMagicBytes();
            if (_dataCrc32 != null)
                _dataCrc32.Append(magicBytes);
            // write header
            FilePosition += ReadWriteHelper.writeRecordAtCurrentPos(_stream, new McapHeader()
            {
                Library = library,
                Profile = profile
            }, _dataCrc32, _options, _context);
        }

        private byte[] writeMagicBytes()
        {

            var ret = new byte[] {137}.Concat(ReadWriteHelper.Encoding.GetBytes("MCAP0\r\n")).ToArray();
            _stream.Write(ret);
            FilePosition += (ulong)ret.Length;
            return ret;
        }

        public void WriteDataRecord(IMcapDataRecord dataRecord)
        {
            if (_context.DataSectionFinished)
                throw new McapWriteException(
                    $"Cannot write into the data section after MCapDataEnd was written or summary section has been started");
            if (dataRecord is McapChunk)
                throw new McapWriteException(
                    $"Cannot write a Chunk directly. Use the 'CreateChunk' method to create a Chunk reference, write records to it, then 'FinishChunk' or dispose it to write to the output");
            if (dataRecord is McapMessageIndex)
                throw new McapWriteException(
                    $"Cannot write a MessageIndex directly. Use the 'CreateChunk' method with storeMessageIndex = true");
            //if (_context.IsInChunk)
            //    throw new McapWriteException(
            //        $"Cannot write into the data section while a Chunk is open. Call 'FinishChunk' or Dispose the existing IMcapChunkWriter to continue writing");
            var startFilePosition = FilePosition;
            FilePosition += ReadWriteHelper.writeRecordAtCurrentPos(_stream, dataRecord, _dataCrc32, _options, _context);

            if (dataRecord is McapAttachment a && _options.AutoSummary?.AutoIndexes?.CreateAttachmentIndexes == true)
                _context.AutoIndexes_AttachmentIndexes.Add(new McapAttachmentIndex()
                {
                    LogTime = a.LogTime,
                    CreateTime = a.CreateTime,
                    Name = a.Name,
                    MediaType = a.MediaType,
                    Offset = startFilePosition,
                    Length = FilePosition - startFilePosition,
                    DataSize = (ulong) a.Data.Length
                });

            if (dataRecord is McapMetadata m && _options.AutoSummary?.AutoIndexes?.CreateMetadataIndex == true)
                _context.AutoIndexes_MetadataIndexes.Add(new McapMetadataIndex()
                {
                    Offset = startFilePosition,
                    Length = FilePosition - startFilePosition,
                    Name = m.Name,
                });
        }

        public void WriteSummaryRecord(IMcapSummaryRecord summaryRecord)
        {
            if (summaryRecord is McapChunkIndex && _options.AutoSummary?.AutoIndexes?.CreateChunkIndexes == true)
                throw new McapWriteException(
                    $"Cannot manually add an McapChunkIndex while 'AutoCreateChunkIndexes' is true");
            if (summaryRecord is McapAttachmentIndex && _options.AutoSummary?.AutoIndexes?.CreateAttachmentIndexes == true)
                throw new McapWriteException(
                    $"Cannot manually add an McapAttachmentIndex while 'AutoCreateAttachmentIndexes' is true");
            if (summaryRecord is McapMetadataIndex && _options.AutoSummary?.AutoIndexes?.CreateMetadataIndex == true)
                throw new McapWriteException(
                    $"Cannot manually add an McapMetadataIndex while 'AutoCreateMetadataIndex' is true");
            if (summaryRecord is McapStatistics && _options.AutoSummary?.AutoStoreStatistics == true)
                throw new McapWriteException(
                    $"Cannot manually add an McapStatistics while 'AutoCreateStatistics' is true");


            writeSummaryRecordInternal(summaryRecord);
        }

        private void writeSummaryRecordInternal(IMcapSummaryRecord summaryRecord)
        {
            //if (_context.IsInChunk)
            //    throw new McapWriteException(
            //        $"Cannot write into the data section while a Chunk is open. Call 'FinishChunk' or Dispose the existing IMcapChunkWriter to continue writing");


            if (!_context.DataSectionFinished)
                WriteDataRecord(new McapDataEnd());
            _context.DataSectionFinished = true;

            if (summaryRecord is McapSummaryOffset so)
            {
                if (_context.SummaryOffsetStart == 0)
                    _context.SummaryOffsetStart = FilePosition;

                // check if summary offset record is indeed correct
                if (!_context.SummaryOffsets.ContainsKey(so.GroupOpCode))
                    throw new McapWriteException(
                        $"Summary Offset record with OpCode: {so.GroupOpCode} is not valid. No such records were written into Summary");
                var offset = _context.SummaryOffsets[so.GroupOpCode];
                if (offset.GroupStart != so.GroupStart || offset.GroupLength != so.GroupLength)
                    throw new McapWriteException(
                        $"Summary Offset record with OpCode: {so.GroupOpCode} is not valid. Length and Offset is not valid");
                FilePosition += ReadWriteHelper.writeRecordAtCurrentPos(_stream, summaryRecord, _summaryCrc32,
                    _options, _context);


            }
            else
            {
                if (_context.SummaryOffsetStart != 0)
                    throw new McapWriteException(
                        "Cannot write summary records. At least one SummaryOffset records were already written");
                if (_context.SummaryStart == 0)
                    _context.SummaryStart = FilePosition;
                if (_context.Summary_FinishedOpCodes.Contains(summaryRecord.Type))
                    throw new McapWriteException(
                        $"Summary records need to be grouped by OpCode. OpCode: {summaryRecord.Type} has already been written");
                if (_context.Summary_LastOpCode != summaryRecord.Type)
                {
                    if (_context.Summary_LastOpCode.HasValue)
                        _context.Summary_FinishedOpCodes.Add(_context.Summary_LastOpCode.Value);
                    _context.Summary_LastOpCode = summaryRecord.Type;
                    _context.SummaryOffsets[(byte)summaryRecord.Type] = new McapSummaryOffset()
                    {
                        GroupOpCode = (byte)summaryRecord.Type,
                        GroupStart = FilePosition
                    };
                }
                var length = ReadWriteHelper.writeRecordAtCurrentPos(_stream, summaryRecord, _summaryCrc32, _options, _context);
                _context.SummaryOffsets[(byte)summaryRecord.Type].GroupLength += length;
                FilePosition += length;
            }

            
            
           

        }

        public IMcapChunkWriter CreateChunk(McapChunkCompression compression, bool storeMessageIndex)
        {
            return new McapChunkWriter(_context, _options, this, compression, storeMessageIndex);
           

        }

        internal void FlushChunk(McapChunkWriter chunkWriter)
        {
            var chunk = new McapChunk()
            {
                MessageStartTime = new McapDateTime(chunkWriter.MessageStartTime),
                MessageEndTime = new McapDateTime(chunkWriter.MessageEndTime),
                UncompressedSize = chunkWriter.UncompressedRecordsSize,
                Compression = chunkWriter.Compression,
                UncompressedCrc = chunkWriter.UncompressedCrc?.GetCurrentHashAsUInt32() ?? 0,
                Write_CompressedStream = chunkWriter.BaseStream
            };
            var chunkStartPosition = FilePosition;
            FilePosition += ReadWriteHelper.writeRecordAtCurrentPos(_stream, chunk, _dataCrc32, _options, _context);
            var chunkLength = FilePosition - chunkStartPosition;
            var messageIndexStartPosition = FilePosition;
            var messageIndexOffsets = new Dictionary<ushort, ulong>();

            if (chunkWriter.StoreMessageIndex)
                foreach (var channelIndex in chunkWriter.MessageIndex)
                {
                    messageIndexOffsets[channelIndex.Key] = FilePosition;
                    FilePosition += ReadWriteHelper.writeRecordAtCurrentPos(_stream, new McapMessageIndex()
                    {
                        ChannelId = channelIndex.Key,
                        Records = channelIndex.Value.ToArray()
                    }, _dataCrc32, _options, _context);
                }
            var messageIndexLength = FilePosition - messageIndexStartPosition;

            if (_options.AutoSummary?.AutoIndexes?.CreateChunkIndexes == true)
                _context.AutoIndexes_ChunkIndexes.Add(new McapChunkIndex()
                {
                    MessageStartTime = chunk.MessageStartTime,
                    MessageEndTime = chunk.MessageEndTime,
                    ChunkStartOffset = chunkStartPosition,
                    ChunkLength = chunkLength,
                    MessageIndexOffsets = messageIndexOffsets,
                    MessageIndexLength = messageIndexLength,
                    Compression = chunk.Compression,
                    CompressedSize = (ulong) chunkWriter.BaseStream.Length,
                    UncompressedSize = chunkWriter.UncompressedRecordsSize

                });
        }
        
        

        private bool _finalized = false;
        public void Finalize(bool closeStream = true, CancellationToken cancelToken = default)
        {
            if (!_finalized)
                _finalized = true;
            else return;
            // auto write summary
            if (_options.AutoSummary != null)
            {
                if (_options.AutoSummary.AutoStoreSchemas)
                    foreach (var sch in _context.Schemas)
                        writeSummaryRecordInternal(sch.Value);
                if (_options.AutoSummary.AutoStoreChannels)
                    foreach (var channel in _context.Channels)
                        writeSummaryRecordInternal(channel.Value);
                if (_options.AutoSummary.AutoIndexes?.CreateAttachmentIndexes == true)
                    foreach (var a in _context.AutoIndexes_AttachmentIndexes)
                        writeSummaryRecordInternal(a);
                if (_options.AutoSummary.AutoIndexes?.CreateMetadataIndex == true)
                    foreach (var a in _context.AutoIndexes_MetadataIndexes)
                        writeSummaryRecordInternal(a);
                if (_options.AutoSummary.AutoIndexes?.CreateChunkIndexes == true)
                    foreach (var a in _context.AutoIndexes_ChunkIndexes)
                        writeSummaryRecordInternal(a);
                if (_options.AutoSummary.AutoStoreStatistics)
                    writeSummaryRecordInternal(new McapStatistics()
                    {
                        MessageCount = _context.Stats_MessageCount,
                        SchemaCount = (ushort)_context.Schemas.Count,
                        ChannelCount = (uint)_context.Channels.Count,
                        AttachmentCount = _context.Stats_AttachmentCount,
                        MetadataCount = _context.Stats_MetadataCount,
                        ChunkCount = _context.Stats_ChunkCount,
                        MessageStartTime = new McapDateTime(_context.Stats_MessageStartTime),
                        MessageEndTime = new McapDateTime(_context.Stats_MessageEndTime),
                        ChannelMessageCounts = _context.Stats_ChannelMessageCounts
                    });
                if (_options.AutoSummary.AutoCreateSummaryOffsets && _context.SummaryOffsets.Count > 0)
                    foreach (var offset in  _context.SummaryOffsets.Values)
                        writeSummaryRecordInternal(offset);
            }
            // write footer
            FilePosition += ReadWriteHelper.writeRecordAtCurrentPos(_stream, new McapFooter()
            {
                SummaryCrc = _summaryCrc32?.GetCurrentHashAsUInt32() ?? 0,
                SummaryStart = _context.SummaryStart,
                SummaryOffsetStart = _context.SummaryOffsetStart
            }, null, _options, _context);
            // write magic bytes
            writeMagicBytes();
            
            if (closeStream)
                _stream.Dispose();
        }

        public void Dispose() => Finalize();


      
    }
}
