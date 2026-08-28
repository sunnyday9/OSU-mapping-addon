using System.Text.Json;

namespace AiStudio.Core.MappingIr.Patterns;

/// <summary>
/// Mania 4K Pattern 强类型参数（ADR-MVP-A-009）。
/// 解决 <see cref="System.Text.Json"/> 反序列化后嵌套值进入 <see cref="JsonElement"/> 导致
/// provider 内 `value is object[]` 失配的隐患（code-review-v0.1 §15）：
/// 解析时同时兼容 CLR 原始类型与 JsonElement。
/// </summary>
public sealed record ManiaPatternParameters(
    string Subdivision,
    double Density,
    string ColumnStrategy,
    IReadOnlyList<int> ColumnOrder,
    double JackTolerance,
    double Bpm,
    int ChordSize,
    double ChordDensity,
    double LnRatio,
    double LnDurationBeats,
    int Count,
    int? JackColumn)
{
    public const string DefaultSubdivision = "1/8";

    public static ManiaPatternParameters Defaults(double bpm)
        => new(
            DefaultSubdivision,
            1.0, // density 默认 1.0 = 全量节奏点（SR 校准旋钮，MVP-B）；<1 才稀疏化
            "alternating",
            new[] { 0, 2, 1, 3 },
            0.05,
            bpm,
            2,
            0.25,
            0.3,
            1.0,
            4,
            null);

    /// <summary>
    /// 从 PatternIntent.Parameters 字典解析（兼容 CLR 原始类型与 JsonElement）。
    /// 缺失/无法解析的字段回退默认值；不抛异常（宽松解析，validator 另行把关）。
    /// <c>density</c> 默认 1.0（全量节奏点）——它是 SR 校准旋钮（MVP-B），
    /// 显式传入 &lt;1 的值才启用稀疏化，保持既有 pattern 行为不变。
    /// </summary>
    public static ManiaPatternParameters FromDictionary(IReadOnlyDictionary<string, object?> dict, double fallbackBpm)
    {
        string subdivision = str(dict, "subdivision") ?? DefaultSubdivision;
        double density = dbl(dict, "density") ?? 1.0;
        string columnStrategy = str(dict, "column_strategy") ?? "alternating";
        IReadOnlyList<int> columnOrder = intList(dict, "column_order") ?? new[] { 0, 2, 1, 3 };
        double jackTolerance = dbl(dict, "jack_tolerance") ?? 0.05;
        double bpm = dbl(dict, "bpm") ?? fallbackBpm;
        int chordSize = int32(dict, "chord_size") ?? 2;
        double chordDensity = dbl(dict, "chord_density") ?? 0.25;
        double lnRatio = dbl(dict, "ln_ratio") ?? 0.3;
        double lnDurationBeats = dbl(dict, "ln_duration_beats") ?? 1.0;
        int count = int32(dict, "count") ?? 4;
        int? jackColumn = int32(dict, "jack_column");

        return new ManiaPatternParameters(
            subdivision,
            density,
            columnStrategy,
            columnOrder,
            jackTolerance,
            bpm,
            chordSize,
            chordDensity,
            lnRatio,
            lnDurationBeats,
            count,
            jackColumn);
    }

    // ---- JSON/CLR 兼容读取 ----

    private static string? str(IReadOnlyDictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v is null)
            return null;
        return v switch
        {
            string s => s,
            JsonElement e when e.ValueKind == JsonValueKind.String => e.GetString(),
            _ => Convert.ToString(v),
        };
    }

    private static double? dbl(IReadOnlyDictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v is null)
            return null;
        return v switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            JsonElement e => e.ValueKind switch
            {
                JsonValueKind.Number when e.TryGetDouble(out double d) => d,
                JsonValueKind.String when double.TryParse(e.GetString(), out double d) => d,
                _ => null,
            },
            _ => null,
        };
    }

    private static int? int32(IReadOnlyDictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v is null)
            return null;
        return v switch
        {
            int i => i,
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            double d when d is >= int.MinValue and <= int.MaxValue => (int)d,
            JsonElement e when e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out int i) => i,
            _ => null,
        };
    }

    private static IReadOnlyList<int>? intList(IReadOnlyDictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v is null)
            return null;
        switch (v)
        {
            case object[] arr:
                return arr.Select(Convert.ToInt32).ToArray();
            case int[] arr:
                return arr;
            case IReadOnlyList<int> list:
                return list;
            case JsonElement e when e.ValueKind == JsonValueKind.Array:
                var result = new List<int>();
                foreach (var item in e.EnumerateArray())
                {
                    if (item.TryGetInt32(out int i))
                        result.Add(i);
                    else
                        return null;
                }

                return result;
            default:
                return null;
        }
    }
}
