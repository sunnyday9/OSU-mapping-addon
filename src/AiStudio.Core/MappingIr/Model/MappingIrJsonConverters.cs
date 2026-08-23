using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiStudio.Core.MappingIr.Model;

/// <summary>
/// 将 null 序列化为空数组（schema 要求 labels 为 array，不允许 null）。
/// 注意：类型参数必须为非空 <see cref="IReadOnlyList{T}"/> 并重写 <see cref="JsonConverter{T}.HandleNull"/>
/// ——否则 System.Text.Json 会在 converter 之前直接把 null 写出去（.NET 8 行为）。
/// </summary>
public sealed class NullToEmptyStringArrayConverter : JsonConverter<IReadOnlyList<string>>
{
    public override bool HandleNull => true;

    public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonSerializer.Deserialize<List<string>?>(ref reader, options) ?? new List<string>();

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteStartArray();
            writer.WriteEndArray();
            return;
        }

        JsonSerializer.Serialize(writer, value.ToList(), options);
    }
}

/// <summary>
/// 将 null 序列化为空 object（schema 要求 difficulty 为非空 object，不允许 null）。
/// </summary>
public sealed class NullToEmptyDictionaryConverter : JsonConverter<IReadOnlyDictionary<string, object?>>
{
    public override bool HandleNull => true;

    public override IReadOnlyDictionary<string, object?> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonSerializer.Deserialize<Dictionary<string, object?>?>(ref reader, options) ?? new Dictionary<string, object?>();

    public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, object?> value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        JsonSerializer.Serialize(writer, new Dictionary<string, object?>(value), options);
    }
}
