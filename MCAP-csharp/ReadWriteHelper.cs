using System;
using System.Buffers;
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
using Microsoft.IO;

namespace MCAP_csharp
{
    internal static class ReadWriteHelper
    {
        internal static readonly Encoding Encoding = Encoding.UTF8;
        private static readonly ArrayPool<byte> _bufferPool;
        private static readonly byte[] _recordTypes = Enum.GetValues(typeof(RecordType)).Cast<byte>().ToArray();
        internal static readonly RecyclableMemoryStreamManager _memStreamManager = new RecyclableMemoryStreamManager();
        static ReadWriteHelper()
        {
            _bufferPool = ArrayPool<byte>.Shared;
        }

        
        internal static ReadBuffer GetBuffer(uint minLength) => new ReadBuffer(minLength, _bufferPool);
        internal static IMcapRecord? readRecordAtCurrentPos(Stream str, RecordType[]? recordTypeFilter)
        {
            using var recordMeta = GetBuffer(9);

            if (str.Read(recordMeta.Buffer, 0, 9) != 9)
                return null;
            var recordType = _recordTypes.Contains(recordMeta.Buffer[0]) ? (RecordType)recordMeta.Buffer[0] : RecordType.Unknown;
            var recordLength = BitConverter.ToInt64(recordMeta.Buffer, 1);
            if (recordTypeFilter != null && !recordTypeFilter.Contains(recordType))
            {
                // if we can seek, seek to the end of record
                if (str.CanSeek)
                    str.Seek(str.Position + recordLength, SeekOrigin.Begin);
                else
                {
                    // if we can't seek, we're in a compressed stream. read recordLength long stuff
                    using var buf = GetBuffer(4096);
                    long read = 0;
                    while (read < recordLength)
                        read += str.Read(buf.Buffer, 0, (int)Math.Min(4096, recordLength - read));
                }
                return null;
            }


            var recordStream = str;
            switch (recordType)
            {
                case RecordType.Header:
                    return new McapHeader()
                    {
                        Profile = (readString(recordStream)).Value,
                        Library = (readString(recordStream)).Value
                    };
                case RecordType.Footer:
                    return new McapFooter()
                    {
                        SummaryStart = (readInt64(recordStream)).Value,
                        SummaryOffsetStart = (readInt64(recordStream)).Value,
                        SummaryCrc = (readInt32(recordStream)).Value
                    };
                case RecordType.Schema:
                    return new McapSchema()
                    {
                        Id = (readInt16(recordStream)).Value,
                        Name = (readString(recordStream)).Value,
                        Encoding = (readString(recordStream)).Value,
                        Data = (readBytes(recordStream)).Value
                    };
                case RecordType.Channel:
                    return new McapChannel()
                    {
                        Id = (readInt16(recordStream)).Value,
                        SchemaId = (readInt16(recordStream)).Value,
                        Topic = (readString(recordStream)).Value,
                        MessageEncoding = (readString(recordStream)).Value,
                        Metadata = (readDict(recordStream, readString, readString)).Value
                    };
                case RecordType.Message:
                    var r = new McapMessage()
                    {
                        ChannelId = (readInt16(recordStream)).Value,
                        Sequence = (readInt32(recordStream)).Value,
                        LogTime = (readDateTime(recordStream)).Value,
                        PublishTime = (readDateTime(recordStream)).Value,
                        Data = (readFixBytes(recordStream, (uint)recordLength - 22)).Value
                    };
                    if (r.PublishTime.ApproximateDateTime is { Year: 1970, Month: 1, Day: 1 })
                        r.PublishTime = r.LogTime;
                    return r;
                case RecordType.Chunk:
                    return new McapChunk()
                    {
                        MessageStartTime = (readDateTime(recordStream)).Value,
                        MessageEndTime = (readDateTime(recordStream)).Value,
                        UncompressedSize = (readInt64(recordStream)).Value,
                        UncompressedCrc = (readInt32(recordStream)).Value,
                        Compression = Enum.TryParse(typeof(McapChunkCompression), (readString(recordStream)).Value, out var c) ? (McapChunkCompression)c : McapChunkCompression.none,
                        Read_RecordsByteLength = (readInt64(recordStream)).Value,
                        Read_RecordsBytesStart = str.Position
                    };
                case RecordType.MessageIndex:
                    return new McapMessageIndex()
                    {
                        ChannelId = (readInt16(recordStream)).Value,
                        Records = (readArray(recordStream, (s) => readTuple(s, readDateTime, readInt64))).Value
                    };
                case RecordType.ChunkIndex:
                    return new McapChunkIndex()
                    {
                        MessageStartTime = (readDateTime(recordStream)).Value,
                        MessageEndTime = (readDateTime(recordStream)).Value,
                        ChunkStartOffset = (readInt64(recordStream)).Value,
                        ChunkLength = (readInt64(recordStream)).Value,
                        MessageIndexOffsets = (readDict(recordStream, readInt16, readInt64)).Value,
                        MessageIndexLength = (readInt64(recordStream)).Value,
                        Compression = Enum.TryParse(typeof(McapChunkCompression), (readString(recordStream)).Value, out var c2) ? (McapChunkCompression)c2 : McapChunkCompression.none,
                        CompressedSize = (readInt64(recordStream)).Value,
                        UncompressedSize = (readInt64(recordStream)).Value,
                    };
                case RecordType.Attachment:
                    return new McapAttachment()
                    {
                        LogTime = (readDateTime(recordStream)).Value,
                        CreateTime = (readDateTime(recordStream)).Value,
                        Name = (readString(recordStream)).Value,
                        MediaType = (readString(recordStream)).Value,
                        Data = (readBytes(recordStream)).Value,
                        Crc = (readInt32(recordStream)).Value
                    };
                case RecordType.Metadata:
                    return new McapMetadata()
                    {
                        Name = (readString(recordStream)).Value,
                        Metadata = (readDict(recordStream, readString, readString)).Value
                    };
                case RecordType.DataEnd:
                    return new McapDataEnd()
                    {
                        DataSectionCrc = (readInt32(recordStream)).Value
                    };
                case RecordType.AttachmentIndex:
                    return new McapAttachmentIndex()
                    {
                        Offset = (readInt64(recordStream)).Value,
                        Length = (readInt64(recordStream)).Value,
                        LogTime = (readDateTime(recordStream)).Value,
                        CreateTime = (readDateTime(recordStream)).Value,
                        DataSize = (readInt64(recordStream)).Value,
                        Name = (readString(recordStream)).Value,
                        MediaType = (readString(recordStream)).Value
                    };
                case RecordType.MetadataIndex:
                    return new McapMetadataIndex()
                    {
                        Offset = (readInt64(recordStream)).Value,
                        Length = (readInt64(recordStream)).Value,
                        Name = (readString(recordStream)).Value
                    };
                case RecordType.Statistics:
                    return new McapStatistics()
                    {
                        MessageCount = (readInt64(recordStream)).Value,
                        SchemaCount = (readInt16(recordStream)).Value,
                        ChannelCount = (readInt32(recordStream)).Value,
                        AttachmentCount = (readInt32(recordStream)).Value,
                        MetadataCount = (readInt32(recordStream)).Value,
                        ChunkCount = (readInt32(recordStream)).Value,
                        MessageStartTime = (readDateTime(recordStream)).Value,
                        MessageEndTime = (readDateTime(recordStream)).Value,
                        ChannelMessageCounts = (readDict(recordStream, readInt16, readInt64)).Value
                    };
                case RecordType.SummaryOffset:
                    var opCode = recordStream.ReadByte();
                    if (opCode == -1)
                        throw new McapReadException("Failed to read OpCode of SummaryOffset. End of stream");
                    return new McapSummaryOffset()
                    {
                        GroupOpCode = (byte)opCode,
                        GroupStart = (readInt64(recordStream)).Value,
                        GroupLength = (readInt64(recordStream)).Value
                    };
                default:
                    return new McapUnknownRecord()
                    {
                        OpCode = recordMeta.Buffer[0]
                    };
            }
        }

        internal static ulong writeRecordAtCurrentPos(Stream str, IMcapRecord record, Crc32? crc32, McapWriterOptions options, McapWriteContext context)
        {
            // get record content stream
            using var recordStream = _memStreamManager.GetStream();
            switch (record.Type)
            {
                case RecordType.Header when record is McapHeader header:
                    write(recordStream, header.Profile);
                    write(recordStream, header.Library);
                    break;
                case RecordType.Footer when record is McapFooter footer:
                    write(recordStream, footer.SummaryStart);
                    write(recordStream, footer.SummaryOffsetStart);
                    write(recordStream, footer.SummaryCrc);
                    break;
                case RecordType.Schema when record is McapSchema sch:
                    // validate schema
                    {
                        if (sch.Id == 0)
                            if (options.IgnoreInvalidSchemaID)
                                return 0;
                            else
                                throw new McapWriteException($"Schema with ID of 0 is invalid and cannot be added to MCAP file");
                        var existing = context.Schemas.GetValueOrDefault(sch.Id);
                        if (existing == null)
                            context.Schemas.Add(sch.Id, sch);
                        else if (!existing.Equals(sch))
                            throw new McapWriteException(
                                $"Schema with ID: {sch.Id} has already been added with different properties");
                    }
                    // write schema
                    write(recordStream, sch.Id);
                    write(recordStream, sch.Name);
                    write(recordStream, sch.Encoding);
                    write(recordStream, sch.Data);
                    break;
                case RecordType.Channel when record is McapChannel channel:
                    // validate channel
                    {
                        if (channel.SchemaId != 0 && !context.Schemas.ContainsKey(channel.SchemaId))
                            throw new McapWriteException(
                                $"Channel with ID: {channel.Id} references Schema ID: {channel.SchemaId} which doesn't exist earlier in the file");

                        var existing = context.Channels.GetValueOrDefault(channel.Id);
                        if (existing == null)
                            context.Channels.Add(channel.Id, channel);
                        else if (!existing.Equals(channel))
                            throw new McapWriteException(
                                $"Channel with ID: {channel.Id} has already been added with different properties");
                    }
                    // write channel
                    write(recordStream, channel.Id);
                    write(recordStream, channel.SchemaId);
                    write(recordStream, channel.Topic);
                    write(recordStream, channel.MessageEncoding);
                    write(recordStream, channel.Metadata, write, write);
                    break;
                case RecordType.Message when record is McapMessage mess:
                    // validate message 
                    {
                        if (!context.Channels.ContainsKey(mess.ChannelId))
                            throw new McapWriteException(
                                $"Message references Channel ID: {mess.ChannelId} which doesn't exist earlier in the file");
                    }
                    // write channel
                    if (mess.PublishTime.ApproximateDateTime is { Year: 1970, Month: 1, Day: 1 })
                        mess.PublishTime = mess.LogTime;
                    write(recordStream, mess.ChannelId);
                    write(recordStream, mess.Sequence);
                    write(recordStream, mess.LogTime);
                    write(recordStream, mess.PublishTime);
                    writeFixBytes(recordStream, mess.Data);
                    // store stats
                    context.Stats_MessageCount++;
                    context.Stats_MessageStartTime = Math.Min(context.Stats_MessageStartTime, mess.LogTime.NanoSeconds);
                    context.Stats_MessageEndTime = Math.Max(context.Stats_MessageEndTime, mess.LogTime.NanoSeconds);
                    if (context.Stats_ChannelMessageCounts.ContainsKey(mess.ChannelId))
                        context.Stats_ChannelMessageCounts[mess.ChannelId]++;
                    else context.Stats_ChannelMessageCounts[mess.ChannelId] = 1;
                    break;
                case RecordType.Chunk when record is McapChunk c:
                    write(recordStream, c.MessageStartTime);
                    write(recordStream, c.MessageEndTime);
                    write(recordStream, c.UncompressedSize);
                    write(recordStream, c.UncompressedCrc);
                    write(recordStream, c.Compression == McapChunkCompression.none ? "": Enum.GetName(typeof(McapChunkCompression), c.Compression)!);
                   
                    write(recordStream, (ulong)c.Write_CompressedStream!.Length);
                    c.Write_CompressedStream.Position = 0;
                    c.Write_CompressedStream.CopyTo(recordStream);
                    // store stats
                    context.Stats_ChunkCount++;
                    break;
                case RecordType.MessageIndex when record is McapMessageIndex mi:
                    write(recordStream, mi.ChannelId);
                    write(recordStream, mi.Records, (stream, t) => write(stream, t, write, write));
                    break;
                case RecordType.ChunkIndex when record is McapChunkIndex ci:
                    write(recordStream, ci.MessageStartTime);
                    write(recordStream, ci.MessageEndTime);
                    write(recordStream, ci.ChunkStartOffset);
                    write(recordStream, ci.ChunkLength);
                    write(recordStream, ci.MessageIndexOffsets, write, write);
                    write(recordStream, ci.MessageIndexLength);
                    write(recordStream, ci.Compression == McapChunkCompression.none ? "" : Enum.GetName(typeof(McapChunkCompression), ci.Compression)!);
                    write(recordStream, ci.CompressedSize);
                    write(recordStream, ci.UncompressedSize);
                    break;
                case RecordType.Attachment when record is McapAttachment att:
                    write(recordStream, att.LogTime);
                    write(recordStream, att.CreateTime);
                    write(recordStream, att.Name);
                    write(recordStream, att.MediaType);
                    write(recordStream, att.Data);
                    // auto calculate attachment crc32 if needed
                    if (att.Crc == 0 && options.Crc32.AutoCalculateAttachmentCrc32)
                        att.Crc = Crc32.HashToUInt32(recordStream.ToArray());
                    write(recordStream, att.Crc);
                    // store stats
                    context.Stats_AttachmentCount++;
                    break;
                case RecordType.Metadata when record is McapMetadata md:
                    write(recordStream, md.Name);
                    write(recordStream, md.Metadata, write, write);
                    // store stats
                    context.Stats_MetadataCount++;
                    break;
                case RecordType.DataEnd when record is McapDataEnd de:
                    // write CRC checksum of data section if needed
                    if (options.Crc32.AutoCalculateDataCrc32 && crc32 != null && de.DataSectionCrc == 0)
                        de.DataSectionCrc = crc32.GetCurrentHashAsUInt32();
                    // we don't need to count this record in any crc checksum
                    crc32 = null;
                    // set data section to finished
                    context.DataSectionFinished = true;
                    write(recordStream, de.DataSectionCrc);
                    break;
                case RecordType.AttachmentIndex when record is McapAttachmentIndex ai:
                    write(recordStream, ai.Offset);
                    write(recordStream, ai.Length);
                    write(recordStream, ai.LogTime);
                    write(recordStream, ai.CreateTime);
                    write(recordStream, ai.DataSize);
                    write(recordStream, ai.Name);
                    write(recordStream, ai.MediaType);
                    break;
                case RecordType.MetadataIndex when record is McapMetadataIndex mi:
                    write(recordStream, mi.Offset);
                    write(recordStream, mi.Length);
                    write(recordStream, mi.Name);
                    break;
                case RecordType.Statistics when record is McapStatistics st:
                    write(recordStream, st.MessageCount);
                    write(recordStream, st.SchemaCount);
                    write(recordStream, st.ChannelCount);
                    write(recordStream, st.AttachmentCount);
                    write(recordStream, st.MetadataCount);
                    write(recordStream, st.ChunkCount);
                    write(recordStream, st.MessageStartTime);
                    write(recordStream, st.MessageEndTime);
                    write(recordStream, st.ChannelMessageCounts, write, write);
                    break;
                case RecordType.SummaryOffset when record is McapSummaryOffset so:
                    write(recordStream, so.GroupOpCode);
                    write(recordStream, so.GroupStart); 
                    write(recordStream, so.GroupLength);
                    break;
                default:
                    throw new McapWriteException(
                        $"Unknown Mcap record: {Enum.GetName(typeof(RecordType), record.Type)}");
            }


            // write record header (type + byte)
            var recordHeader = new [] {(byte) record.Type}.Concat(BitConverter.GetBytes(recordStream.Length))
                .ToArray();
            str.Write(recordHeader);
            recordStream.Position = 0;
            recordStream.CopyTo(str);
            if (crc32 != null)
            {
                crc32.Append(recordHeader);
                recordStream.Position = 0;
                crc32.Append(recordStream);
            }

            return (ulong)recordHeader.Length + (ulong)recordStream.Length;

        }

        private static ReadResult<ushort> readInt16(Stream str)
        {
            using var b = GetBuffer(2);
            if (str.Read(b.Buffer, 0, 2) != 2)
                throw new McapReadException(
                    $"Failed to read Int16. Failed to read 2 bytes");
            return new ReadResult<ushort>(2, BitConverter.ToUInt16(b.Buffer, 0));
        }
        private static ReadResult<uint> readInt32(Stream str)
        {
            using var b = GetBuffer(4);
            if (str.Read(b.Buffer, 0, 4) != 4)
                throw new McapReadException(
                    $"Failed to read Int32. Failed to read 4 bytes");
            return new ReadResult<uint>(4, BitConverter.ToUInt32(b.Buffer, 0));
        }
        private static ReadResult<ulong> readInt64(Stream str)
        {
            using var b = GetBuffer(8);
            if (str.Read(b.Buffer, 0, 8) != 8)
                throw new McapReadException(
                    $"Failed to read Int64. Failed to read 8 bytes");
            return new ReadResult<ulong>(8, BitConverter.ToUInt64(b.Buffer, 0));
        }
        private static ReadResult<string> readString(Stream str)
        {
            var readLength = readInt32(str);
            using var b = GetBuffer(readLength.Value);
            if (str.Read(b.Buffer, 0, (int)readLength.Value) != readLength.Value)
                throw new McapReadException(
                    $"Failed to read String. Failed to read {readLength.Value} bytes");
            return new ReadResult<string>(readLength.Read + readLength.Value, Encoding.GetString(b.Buffer, 0, (int)readLength.Value));
        }
        private static ReadResult<byte[]> readBytes(Stream str)
        {
            var readLength = readInt32(str);
            var b = new byte[readLength.Value];
            if (str.Read(b, 0, (int)readLength.Value) != readLength.Value)
                throw new McapReadException(
                    $"Failed to read Bytes. Failed to read {readLength.Value} bytes");
            return new ReadResult<byte[]>(readLength.Read + readLength.Value, b);
        }
        private static ReadResult<byte[]> readFixBytes(Stream str, uint length)
        {
            var b = new byte[length];
            if (str.Read(b, 0, (int)length) != length)
                throw new McapReadException(
                    $"Failed to read Bytes. Failed to read {length} bytes");
            return new ReadResult<byte[]>(length, b);
        }
        private static ReadResult<McapDateTime> readDateTime(Stream str)
        {
            var readLong = readInt64(str);
            return new ReadResult<McapDateTime>(readLong.Read, new McapDateTime(readLong.Value));
        }
        private static ReadResult<Dictionary<TKey, TValue>> readDict<TKey, TValue>(Stream str,
            Func<Stream, ReadResult<TKey>> readKey, Func<Stream, ReadResult<TValue>> readValue)
        {
            var readLength = readInt32(str);
            long read = 0;
            var ret = new Dictionary<TKey, TValue>();
            while (read < readLength.Value)
            {
                var key = readKey(str);
                var val = readValue(str);
                ret.Add(key.Value, val.Value);
                read += key.Read + val.Read;
            }
            return new ReadResult<Dictionary<TKey, TValue>>(readLength.Read + read, ret);
        }
        private static ReadResult<T[]> readArray<T>(Stream str, Func<Stream, ReadResult<T>> readValue)
        {
            var readLength = readInt32(str);
            long read = 0;
            var ret = new List<T>();
            while (read < readLength.Value)
            {
                var i = readValue(str);
                read += i.Read;
                ret.Add(i.Value);
            }
            return new ReadResult<T[]>(readLength.Read + read, ret.ToArray());
        }
        private static ReadResult<Tuple<T1, T2>> readTuple<T1, T2>(Stream str,
            Func<Stream, ReadResult<T1>> readVal1, Func<Stream, ReadResult<T2>> readVal2)
        {
            var val1 = readVal1(str);
            var val2 = readVal2(str);
            return new ReadResult<Tuple<T1, T2>>(val1.Read + val2.Read, new Tuple<T1, T2>(val1.Value, val2.Value));
        }
        private struct ReadResult<T>
        {
            public readonly T Value;
            public readonly long Read;

            public ReadResult(long read, T value)
            {
                Read = read;
                Value = value;
            }
        }

        private static void write(MemoryStream str, byte val) =>
            str.WriteByte(val);
        private static void write(MemoryStream str, ushort val) =>
            str.Write(BitConverter.GetBytes(val));

        private static void write(MemoryStream str, uint val) =>
            str.Write(BitConverter.GetBytes(val));
        private static void write(MemoryStream str, ulong val) =>
            str.Write(BitConverter.GetBytes(val));

        private static void write(MemoryStream str, string val)
        {
            var strBytes = Encoding.GetBytes(val);
            write(str, (uint)strBytes.Length);
            str.Write(strBytes);
        }
        private static void write(MemoryStream str, byte[] val)
        {
            write(str, (uint)val.Length);
            str.Write(val);
        }
        private static void writeFixBytes(MemoryStream str, byte[] val) => str.Write(val);
        private static void write(MemoryStream str, McapDateTime val) => write(str, val.NanoSeconds);

        private static void write<TKey, TValue>(MemoryStream str, Dictionary<TKey, TValue> dict,
            Action<MemoryStream, TKey> writeKey,
            Action<MemoryStream, TValue> writeValue)
        {
            using var valueStream = _memStreamManager.GetStream();
            foreach (var v in dict)
            {
                writeKey(valueStream, v.Key);
                writeValue(valueStream, v.Value);
            }
            write(str, (uint)valueStream.Length);
            valueStream.Position = 0;
            valueStream.CopyTo(str);
        }

        private static void write<T>(MemoryStream str, T[] val, Action<MemoryStream, T> writeItem)
        {
            using var valueStream = _memStreamManager.GetStream();
            foreach (var v in val)
                writeItem(valueStream, v);
            write(str, (uint)valueStream.Length);
            valueStream.Position = 0;
            valueStream.CopyTo(str);
        }

        private static void write<T1, T2>(MemoryStream str, Tuple<T1, T2> val, Action<MemoryStream, T1> writeVal1,
            Action<MemoryStream, T2> writeVal2)
        {
            writeVal1(str, val.Item1);
            writeVal2(str, val.Item2);
        }
    }

    public class ReadBuffer : IDisposable
    {
        public readonly byte[] Buffer;
        private ArrayPool<byte> pool;
        public ReadBuffer(uint minLength, ArrayPool<byte> pool)
        {
            Buffer = pool.Rent((int)minLength);
            this.pool = pool;
        }

        public void Dispose()
        {
            pool.Return(Buffer);
        }
    }
}
