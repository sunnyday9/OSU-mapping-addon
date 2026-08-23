using System.Text.Json;
using System.Text.Json.Serialization;
using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.Serialization;

/// <summary>
/// Mapping IR v0.1 文档序列化器。
/// 命名策略：snake_case（对齐 <c>mapping-ir-v0.1.schema.json</c>）；枚举：snake_case 字符串。
/// </summary>
public sealed class JsonMappingIrSerializer
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize(MappingDocument document)
        => JsonSerializer.Serialize(document, Options);

    public static MappingDocument? Deserialize(string json)
        => JsonSerializer.Deserialize<MappingDocument>(json, Options);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
            // schema 要求 style/provenance.human_edits 等键存在（可 null），因此不整体忽略 null；
            // 仅省略次要可空字段（map.title 等）由类型默认值承担。
        };

        // 枚举统一 snake_case 字符串；object? 属性按原始 JSON 透传。
        options.Converters.Add(new SnakeCaseStringEnumConverter());
        return options;
    }
}
