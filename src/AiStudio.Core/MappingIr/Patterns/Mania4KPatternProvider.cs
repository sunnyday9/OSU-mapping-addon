using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.Patterns;

/// <summary>
/// Mania 4K Pattern Provider：实现 4K MVP Pattern 集（mania-pattern-grammar-v0.1 §16）：
/// single / stream / burst / jack / jump / jumpstream / single_ln / ln_rice / ln_release。
/// 所有 family 的节奏时间轴从 beat 网格派生（beat index × 细分），保证对象落在量化网格上；
/// 生成严格确定性（固定 seed + 固定输入）。
/// </summary>
public sealed class Mania4KPatternProvider : IPatternProvider
{
    public const int KeyCount = 4;

    public RulesetKind Ruleset => RulesetKind.Mania;

    public PatternGenerationResult Generate(PatternIntent intent, PatternGenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(context);

        if (intent.Ruleset != RulesetKind.Mania)
            return new PatternGenerationResult(Array.Empty<ConcreteObject>(), new[] { new PatternIssue("ruleset_mismatch", "error", $"Provider is Mania but intent targets {intent.Ruleset}.") });

        var rng = context.CreateFamilyRandom(intent.Family);
        var rhythm = new RhythmTimeline(intent, context.Music);

        IReadOnlyList<ConcreteObject> objects = intent.Family switch
        {
            "single" => generateSingle(intent, rhythm, rng),
            "stream" => generateStream(intent, rhythm, rng),
            "burst" => generateBurst(intent, rhythm, rng),
            "jack" => generateJack(intent, rhythm, rng),
            "jump" => generateJump(intent, rhythm, rng),
            "jumpstream" => generateJumpstream(intent, rhythm, rng),
            "single_ln" => generateSingleLn(intent, rhythm, rng),
            "ln_rice" => generateLnRice(intent, rhythm, rng),
            "ln_release" => generateLnRelease(intent, rhythm, rng),
            _ => Array.Empty<ConcreteObject>(),
        };

        var issues = objects.Count == 0
            ? new[] { new PatternIssue("unknown_family", "error", $"Unknown family '{intent.Family}' or no objects generated.") }
            : Array.Empty<PatternIssue>();

        return new PatternGenerationResult(objects, issues);
    }

    // ---- rhythm timeline -------------------------------------------------

    /// <summary>
    /// 基于 beat 网格的节奏时间轴：subdivision 决定每拍细分份数，
    /// 时间 = beatStart + (subIndex / subdivision) × beatMs，消除跨拍累积误差。
    /// </summary>
    private sealed class RhythmTimeline
    {
        private readonly double beatMs;
        private readonly int subdivision;
        private readonly int step; // 每步的细分份数（LN family 用大步长避免同列重叠）

        public double BeatMs { get; }

        public RhythmTimeline(PatternIntent intent, MusicTimeline music)
        {
            double bpm = intent.Parameters.TryGetValue("bpm", out var v) && v is not null
                ? Convert.ToDouble(v)
                : music.Tempo.BaseBpm;
            if (bpm <= 0)
                bpm = 180.0;

            BeatMs = 60000.0 / bpm;
            beatMs = BeatMs;

            string sub = intent.Parameters.TryGetValue("subdivision", out var sv) ? Convert.ToString(sv) ?? "1/8" : "1/8";
            subdivision = sub switch
            {
                "1/1" => 1,
                "1/2" => 2,
                "1/4" => 4,
                "1/8" => 8,
                "1/12" => 12,
                "1/16" => 16,
                "1/24" => 24,
                _ => 8,
            };

            // LN family：每步至少跨 1 拍（subdivision 份），保证同列 LN 不相交。
            // 例：1/8 细分 + LN → 每 8 个 1/8 拍放一个对象（步长 = 1 拍）。
            bool isLnFamily = intent.Family is "single_ln" or "ln_rice" or "ln_release";
            step = isLnFamily ? subdivision : 1;
        }

        /// <summary>从 start 起第 i 个节奏点（对齐 beat 网格：先归位到最近 beat 起点）。</summary>
        public int Time(PatternIntent intent, int i)
        {
            double alignedStart = Math.Round(intent.StartTime / beatMs) * beatMs;
            return (int)Math.Round(alignedStart + (double)i * step * beatMs / subdivision);
        }
    }

    // ---- parameter helpers ------------------------------------------------

    private static int[] columnOrder(PatternIntent intent)
    {
        if (intent.Parameters.TryGetValue("column_order", out var value) && value is object[] arr && arr.Length > 0)
            return arr.Select(Convert.ToInt32).ToArray();
        return new[] { 0, 2, 1, 3 };
    }

    private static int? jackColumn(PatternIntent intent)
    {
        if (intent.Parameters.TryGetValue("jack_column", out var value) && value is not null)
            return Convert.ToInt32(value);
        return null;
    }

    private static int chordSize(PatternIntent intent)
    {
        if (intent.Parameters.TryGetValue("chord_size", out var v) && v is not null)
            return Math.Clamp(Convert.ToInt32(v), 1, KeyCount);
        return 2;
    }

    private static double chordDensity(PatternIntent intent)
        => intent.Parameters.TryGetValue("chord_density", out var v) && v is not null
            ? Math.Clamp(Convert.ToDouble(v), 0.0, 1.0)
            : 0.25;

    private static double lnDurationMs(PatternIntent intent, RhythmTimeline rhythm)
    {
        if (intent.Parameters.TryGetValue("ln_duration_beats", out var v) && v is not null)
            return Math.Max(Convert.ToDouble(v), 0.25) * rhythm.BeatMs;
        return rhythm.BeatMs;
    }

    private static double lnRatio(PatternIntent intent)
        => intent.Parameters.TryGetValue("ln_ratio", out var v) && v is not null
            ? Math.Clamp(Convert.ToDouble(v), 0.0, 1.0)
            : 0.3;

    // ---- generators -----------------------------------------------------

    private static List<ConcreteObject> generateSingle(PatternIntent intent, RhythmTimeline rhythm, Random rng)
    {
        var order = columnOrder(intent);
        var result = new List<ConcreteObject>();
        int i = 0;
        while (rhythm.Time(intent, i) <= intent.EndTime)
        {
            int col = order[i % order.Length];
            result.Add(new ConcreteObject($"n{result.Count + 1}", "hit", rhythm.Time(intent, i), Column: col, SourcePatternId: intent.Id));
            i++;
        }

        return result;
    }

    private static List<ConcreteObject> generateStream(PatternIntent intent, RhythmTimeline rhythm, Random rng)
        => generateSingle(intent, rhythm, rng);

    private static List<ConcreteObject> generateBurst(PatternIntent intent, RhythmTimeline rhythm, Random rng)
    {
        int count = intent.Parameters.TryGetValue("count", out var v) && v is not null
            ? Math.Clamp(Convert.ToInt32(v), 2, 12)
            : 4;
        var order = columnOrder(intent);
        var result = new List<ConcreteObject>();
        for (int i = 0; i < count; i++)
        {
            int col = order[i % order.Length];
            result.Add(new ConcreteObject($"n{result.Count + 1}", "hit", rhythm.Time(intent, i), Column: col, SourcePatternId: intent.Id));
        }

        return result;
    }

    private static List<ConcreteObject> generateJack(PatternIntent intent, RhythmTimeline rhythm, Random rng)
    {
        int count = intent.Parameters.TryGetValue("count", out var v) && v is not null
            ? Math.Clamp(Convert.ToInt32(v), 2, 12)
            : 4;
        int col = jackColumn(intent) ?? 0;
        var result = new List<ConcreteObject>();
        for (int i = 0; i < count; i++)
        {
            result.Add(new ConcreteObject($"n{result.Count + 1}", "hit", rhythm.Time(intent, i), Column: col, SourcePatternId: intent.Id));
        }

        return result;
    }

    private static List<ConcreteObject> generateJump(PatternIntent intent, RhythmTimeline rhythm, Random rng)
    {
        int size = chordSize(intent);
        var order = columnOrder(intent);
        var result = new List<ConcreteObject>();
        int i = 0;
        while (rhythm.Time(intent, i) <= intent.EndTime)
        {
            foreach (int col in pickChord(order, size, i, rng))
                result.Add(new ConcreteObject($"n{result.Count + 1}", "hit", rhythm.Time(intent, i), Column: col, SourcePatternId: intent.Id));
            i++;
        }

        return result;
    }

    private static List<ConcreteObject> generateJumpstream(PatternIntent intent, RhythmTimeline rhythm, Random rng)
    {
        double density = chordDensity(intent);
        int size = chordSize(intent);
        var order = columnOrder(intent);
        var result = new List<ConcreteObject>();
        int i = 0;
        while (rhythm.Time(intent, i) <= intent.EndTime)
        {
            bool chord = rng.NextDouble() < density;
            if (chord)
            {
                foreach (int col in pickChord(order, size, i, rng))
                    result.Add(new ConcreteObject($"n{result.Count + 1}", "hit", rhythm.Time(intent, i), Column: col, SourcePatternId: intent.Id));
            }
            else
            {
                int col = order[i % order.Length];
                result.Add(new ConcreteObject($"n{result.Count + 1}", "hit", rhythm.Time(intent, i), Column: col, SourcePatternId: intent.Id));
            }

            i++;
        }

        return result;
    }

    private static List<ConcreteObject> generateSingleLn(PatternIntent intent, RhythmTimeline rhythm, Random rng)
    {
        double durationMs = lnDurationMs(intent, rhythm);
        var order = columnOrder(intent);
        var result = new List<ConcreteObject>();
        int i = 0;
        while (rhythm.Time(intent, i) <= intent.EndTime)
        {
            int col = order[i % order.Length];
            int t = rhythm.Time(intent, i);
            result.Add(new ConcreteObject($"n{result.Count + 1}", "hold", t, EndTime: t + (int)Math.Round(durationMs), Column: col, SourcePatternId: intent.Id));
            i++;
        }

        return result;
    }

    private static List<ConcreteObject> generateLnRice(PatternIntent intent, RhythmTimeline rhythm, Random rng)
    {
        double durationMs = lnDurationMs(intent, rhythm);
        double ratio = lnRatio(intent);
        var order = columnOrder(intent);
        var result = new List<ConcreteObject>();
        int i = 0;
        while (rhythm.Time(intent, i) <= intent.EndTime)
        {
            bool isLn = rng.NextDouble() < ratio;
            int t = rhythm.Time(intent, i);
            if (isLn)
            {
                int col = order[i % order.Length];
                result.Add(new ConcreteObject($"n{result.Count + 1}", "hold", t, EndTime: t + (int)Math.Round(durationMs), Column: col, SourcePatternId: intent.Id));
            }
            else
            {
                int col = order[(i + 1) % order.Length];
                result.Add(new ConcreteObject($"n{result.Count + 1}", "hit", t, Column: col, SourcePatternId: intent.Id));
            }

            i++;
        }

        return result;
    }

    private static List<ConcreteObject> generateLnRelease(PatternIntent intent, RhythmTimeline rhythm, Random rng)
    {
        double durationMs = lnDurationMs(intent, rhythm);
        var order = columnOrder(intent);
        var result = new List<ConcreteObject>();
        int i = 0;
        while (rhythm.Time(intent, i) <= intent.EndTime)
        {
            int col = order[i % order.Length];
            // release pattern: LN 结束时间精确落在后续节奏点上（不静默移动）。
            int t = rhythm.Time(intent, i);
            result.Add(new ConcreteObject($"n{result.Count + 1}", "hold", t, EndTime: t + (int)Math.Round(durationMs), Column: col, SourcePatternId: intent.Id));
            i++;
        }

        return result;
    }

    // ---- column helpers -------------------------------------------------

    private static int[] pickChord(int[] order, int size, int index, Random rng)
    {
        size = Math.Clamp(size, 2, KeyCount);
        if (size >= KeyCount)
            return new[] { 0, 1, 2, 3 };

        // 确定性 + 多样性：从列集中选相邻列对（0-1 / 1-2 / 2-3），按 index 轮换，rng 做起始偏移。
        int start = (index + rng.Next(KeyCount - size + 1)) % (KeyCount - size + 1);
        var cols = new List<int>();
        for (int k = 0; k < size; k++)
            cols.Add(order[(start + k) % order.Length]);
        return cols.Distinct().ToArray();
    }
}
