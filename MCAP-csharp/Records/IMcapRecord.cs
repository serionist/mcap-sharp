using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace MCAP_csharp.Records
{
    public interface IMcapRecord
    {
        RecordType Type { get; }
    }

    public interface IMcapDataRecord: IMcapRecord
    {

    }

    public interface IMcapSummaryRecord: IMcapRecord
    {

    }
    public interface IMcapChunkContentRecord: IMcapDataRecord
    {

    }
    
    public static class McapRecordExtensions
    {
        public static string ToJson(this IMcapRecord record, JsonSerializerOptions? jsonOptions = null) =>
            JsonSerializer.Serialize(record, record.GetType(), jsonOptions);

        public static T ToMcapRecord<T>(this string str, JsonSerializerOptions? jsonOptions = null)
            where T : IMcapRecord => JsonSerializer.Deserialize<T>(str, jsonOptions)!;

    }
}
