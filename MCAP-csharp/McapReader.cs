using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MCAP_csharp.DataTypes;
using MCAP_csharp.Exceptions;
using MCAP_csharp.Records;
using ZstdSharp;
using K4os.Compression.LZ4.Streams;
using MCAP_csharp.Reader;


namespace MCAP_csharp
{
    public class McapReader
    {
        private readonly Stream _stream;
       
        public McapHeader Header { get; private set; }
        public McapFooter Footer { get; private set; }

        private ulong DataStart { get; set; }
        private ulong DataEnd { get; set; }

        private McapReader(Stream stream)
        {
            _stream = stream;
           Header = new McapHeader();
           Footer = new McapFooter();
        }
        public static McapReader FromStream(Stream stream)
        {
            var ret = new McapReader(stream);
            ret.initialize();
            return ret;
        }
        private void initialize()
        {
            if (!_stream.CanRead)
                throw new InvalidOperationException("McapReader requires 'CanRead' on source stream");
            if (!_stream.CanSeek)
                throw new InvalidOperationException("McapReader requires 'CanSeek' on source stream");
            // check magic bytes at the start

            // read start magic bytes
            _stream.Seek(0, SeekOrigin.Begin);
            checkMagicBytes("start");
            // read header record
            var headerRecord = ReadWriteHelper.readRecordAtCurrentPos(_stream, new[] {RecordType.Header});
            if (!(headerRecord is McapHeader h))
                throw new InvalidMcapFormatException($"First record is not 'Header'");
            Header = h;
            DataStart = (ulong)_stream.Position;
            // read footer record
            _stream.Seek(-37, SeekOrigin.End);
            var footerStartPos = (ulong)_stream.Position;
            var footerRecord = ReadWriteHelper.readRecordAtCurrentPos(_stream, new[] {RecordType.Footer});
            if (!(footerRecord is McapFooter f))
                throw new InvalidMcapFormatException($"Last record is not 'Footer'");
            Footer = f;
            if (Footer.SummaryStart != 0)
                DataEnd = Footer.SummaryStart;
            else if (Footer.SummaryOffsetStart != 0)
                DataEnd = Footer.SummaryOffsetStart;
            else DataEnd = footerStartPos;


            // read end magic bytes (stream should be in this position)
            checkMagicBytes("end");
        }
        private void checkMagicBytes(string errFragment)
        {
            using var buf = ReadWriteHelper.GetBuffer(8);
            var read = _stream.Read(buf.Buffer, 0, 8);
            if (read != 8)
                throw new InvalidMcapFormatException(
                    $"Magic bytes are invalid at file {errFragment}. File is not long enough");
            if (buf.Buffer[0] != 137 || ReadWriteHelper.Encoding.GetString(buf.Buffer, 1, 4) != "MCAP" ||
                ReadWriteHelper.Encoding.GetString(buf.Buffer, 6, 2) != "\r\n")
                throw new InvalidMcapFormatException($"Magic bytes are invalid at file {errFragment}");
            if (ReadWriteHelper.Encoding.GetString(buf.Buffer, 5, 1) != "0")
                throw new InvalidMcapFormatException(
                    $"Magic bytes are invalid at file {errFragment}. Major version '{ReadWriteHelper.Encoding.GetString(buf.Buffer, 5, 1)} is not supported'");
        }
        
        public IEnumerable<IMcapDataRecord> ReadDataRecords(RecordType[]? recordTypeFilter = null, ulong? byteOffsetFromStartOfFile = null, ulong? maxBytesToRead = null, [EnumeratorCancellation]CancellationToken cancelToken = default)
        {
            if (byteOffsetFromStartOfFile.HasValue)
                _stream.Seek((long)byteOffsetFromStartOfFile.Value, SeekOrigin.Begin);
            else 
                _stream.Seek((long)DataStart, SeekOrigin.Begin);

            var maxLength = DataEnd;
            if (maxBytesToRead.HasValue)
                maxLength = (ulong)_stream.Position + maxBytesToRead.Value;
            while ((ulong)_stream.Position < maxLength && !cancelToken.IsCancellationRequested)
            {
                var record = ReadWriteHelper.readRecordAtCurrentPos(_stream, recordTypeFilter);
                if (record == null)
                    continue;
                if (!(record is IMcapDataRecord dataRecord))
                    yield break;
                yield return dataRecord;
                if (record.Type == RecordType.DataEnd)
                    yield break;

            }

        }
        public IEnumerable<IMcapSummaryRecord> ReadSummaryRecords(RecordType[]? recordTypeFilter = null, ulong? byteOffsetFromStartOfFile = null, ulong? maxBytesToRead = null, [EnumeratorCancellation] CancellationToken cancelToken = default)
        {
            if (Footer.SummaryStart == 0)
                yield break;
            long startPos = (long)Footer.SummaryStart;
            if (byteOffsetFromStartOfFile.HasValue)
                startPos = Math.Max(startPos, (long)byteOffsetFromStartOfFile.Value);

            _stream.Seek((long)startPos, SeekOrigin.Begin);
            var strLength = _stream.Length;
            if (maxBytesToRead.HasValue)
                strLength = _stream.Position + (long)maxBytesToRead.Value;
            while (_stream.Position < strLength && !cancelToken.IsCancellationRequested)
            {
                var record = ReadWriteHelper.readRecordAtCurrentPos(_stream, recordTypeFilter);
                if (record == null)
                    continue;
                if (!(record is IMcapSummaryRecord r))
                    yield break;
                yield return r;

            }

        }
        public IEnumerable<IMcapChunkContentRecord> ReadChunkRecords(McapChunk chunk, ulong? byteOffsetFromChunkUncompressedData = null, ulong? maxBytesToRead = null, RecordType[]? recordTypeFilter = null, [EnumeratorCancellation] CancellationToken cancelToken = default)
        {
            _stream.Seek((long)chunk.Read_RecordsBytesStart, SeekOrigin.Begin);

            // uncompress the chunk into a read stream -> its better to uncompress the entire chunk into a memory stream, rather than decompress on the fly, as algorithms can process the compressed data frames on their own buffers. 
            // decompressing on the fly causes some issues with lz4.
            Stream chunkReadStream;
            {
                var chunkStream = PartialReadStream.From(_stream, chunk.Read_RecordsBytesStart, (long)chunk.Read_RecordsByteLength, false);
                if (chunk.Compression == McapChunkCompression.none)
                    chunkReadStream = chunkStream;
                else
                {
                    chunkReadStream = ReadWriteHelper._memStreamManager.GetStream();
                    switch (chunk.Compression)
                    {
                        case McapChunkCompression.zstd:
                            using (var zstdStream = new DecompressionStream(chunkStream))
                                zstdStream.CopyTo(chunkReadStream);
                            break;
                        case McapChunkCompression.lz4:
                            using (var lz4Stream = LZ4Stream.Decode(chunkStream))
                                lz4Stream.CopyTo(chunkReadStream);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    chunkStream.Dispose();
                }
            }


            var maxLength = (long) chunkReadStream.Length;
            if (maxBytesToRead.HasValue)
                maxLength = (long)maxBytesToRead.Value;


            if (byteOffsetFromChunkUncompressedData.HasValue)
                chunkReadStream.Seek((long) byteOffsetFromChunkUncompressedData.Value, SeekOrigin.Begin);
            else chunkReadStream.Seek(0, SeekOrigin.Begin);

            while (chunkReadStream.Position < maxLength && !cancelToken.IsCancellationRequested)
            {
                var record = ReadWriteHelper.readRecordAtCurrentPos(chunkReadStream, recordTypeFilter);
                if (record == null)
                    continue;
                if (!(record is IMcapChunkContentRecord dataRecord))
                    throw new McapReadException(
                        $"Chunk contains invalid record type: {Enum.GetName(typeof(RecordType), record?.Type ?? RecordType.Unknown)}");
                yield return dataRecord;

            }

        }


        private McapSummaryOffset[]? _summaryOffsets = null;
        public McapSummaryOffset[] Summary_Offsets(CancellationToken cancelToken = default)
        {
            if (_summaryOffsets == null)
                if (Footer.SummaryOffsetStart == 0)
                    _summaryOffsets = Array.Empty<McapSummaryOffset>();
                else
                    _summaryOffsets = ReadSummaryRecords( new[]
                    {
                        RecordType.SummaryOffset
                    }, Footer.SummaryOffsetStart, null, cancelToken).Cast<McapSummaryOffset>().ToArray();
            return _summaryOffsets;
        }

        public T[] Summary_GetRecords<T>(CancellationToken cancelToken = default)
            where T : IMcapSummaryRecord, new()
        {
            var type = new T().Type;
            var offsets = Summary_Offsets(cancelToken);
            var schemaOffset = offsets.FirstOrDefault(e => e.GroupOpCode == (byte)type);
            return ReadSummaryRecords(new[] { type }, schemaOffset?.GroupStart,
                schemaOffset?.GroupLength, cancelToken).Cast<T>().ToArray();
        }

        public McapStatistics? Summary_GetStatistics(CancellationToken cancelToken = default) =>
            (Summary_GetRecords<McapStatistics>(cancelToken)).FirstOrDefault();





    }
}
