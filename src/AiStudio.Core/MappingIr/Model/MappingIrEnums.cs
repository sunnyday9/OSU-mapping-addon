using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiStudio.Core.MappingIr.Model;

/// <summary>
/// snake_case 字符串枚举转换器（对齐 mapping-ir-v0.1.schema.json 的 enum 值，如 PreChorus → "pre_chorus"）。
/// </summary>
public sealed class SnakeCaseStringEnumConverter : JsonStringEnumConverter
{
    public SnakeCaseStringEnumConverter()
        : base(JsonNamingPolicy.SnakeCaseLower)
    {
    }
}
