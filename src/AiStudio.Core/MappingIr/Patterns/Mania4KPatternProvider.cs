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
        var parameters = ManiaPatternParameters.FromDictionary(intent.Parameters, context.Music.Tempo.BaseBpm > 0 ? context.Music.Tempo.BaseBpm : 180.0);
        bool isLnFamily = intent.Family is "single_ln" or "ln_rice" or "ln_release";
        var rhythm = new RhythmTimeline(parameters, context.Music, isLnFamily);

        IReadOnlyList<ConcreteObject> objects = intent.Family switch
        {
            "single" => generateSingle(intent, parameters, rhythm, rng),
            "stream" => generateStream(intent, parameters, rhythm, rng),
            "burst" => generateBurst(intent, parameters, rhythm, rng),
            "jack" => generateJack(intent, parameters, rhythm, rng),
            "jump" => generateJump(intent, parameters, rhythm, rng),
            "jumpstream" => generateJumpstream(intent, parameters, rhythm, rng),
            "single_ln" => generateSingleLn(intent, parameters, rhythm, rng),
            "ln_rice" => generateLnRice(intent, parameters, rhythm, rng),
            "ln_release" => generateLnRelease(intent, parameters, rhythm, rng),
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
    /// <see cref="densityRatio"/>（0–1] 稀疏化节奏点：ratio 1.0 全量，0.5 隔点取一——
    /// 这是 SR 校准（MVP-B）的连续密度旋钮：floor((i+1)r) > floor(ir) 的确定性均匀采样
    /// 使对象数随 ratio 连续单调（无平台区），且完全确定性。
    /// </summary>
    private sealed class RhythmTimeline
    {
        private readonly double beatMs;
        private readonly int subdivision;
        private readonly int step; // 每步的细分份数（LN family 用大步长避免同列重叠）
        private readonly double densityRatio; // (0,1]：保留的节奏点比例

        public double BeatMs { get; }

        public RhythmTimeline(ManiaPatternParameters parameters, MusicTimeline music, bool isLnFamily)
        {
            double bpm = parameters.Bpm > 0 ? parameters.Bpm : music.Tempo.BaseBpm;
            if (bpm <= 0)
                bpm = 180.0;

            BeatMs = 60000.0 / bpm;
            beatMs = BeatMs;

            subdivision = parameters.Subdivision switch
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
            step = isLnFamily ? subdivision : 1;

            densityRatio = Math.Clamp(parameters.Density > 0 ? parameters.Density : 1.0, 0.05, 1.0);
        }

        /// <summary>从 start 起第 i 个节奏点（对齐 beat 网格：先归位到最近 beat 起点）。</summary>
        public int Time(PatternIntent intent, int i)
        {
            double alignedStart = Math.Round(intent.StartTime / beatMs) * beatMs;
            return (int)Math.Round(alignedStart + (double)i * step * beatMs / subdivision);
        }

        /// <summary>是否保留第 i 个节奏点（densityRatio 稀疏化，确定性：i 是整数索引）。</summary>
        public bool Keep(int i)
            // floor((i+1)×ratio) > floor(i×ratio)：精确 ratio 比例的确定性均匀采样。
            // 无 rng、无浮点累积误差（每步独立计算），ratio 单调 → 保留数单调。
            => Math.Floor((i + 1) * densityRatio) > Math.Floor(i * densityRatio);
    }

    // ---- generators -----------------------------------------------------

    private static List<ConcreteObject> generateSingle(PatternIntent intent, ManiaPatternParameters parameters, RhythmTimeline rhythm, Random rng)
    {
        var order = parameters.ColumnOrder;
        var result = new List<ConcreteObject>();
        int i = 0;
        while (rhythm.Time(intent, i) <= intent.EndTime)
        {
            if (!rhythm.Keep(i))
            {
                i++;
                continue;
            }

            int col = order[i % order.Count];
            result.Add(new ConcreteObject($"n{result.Count + 1}", "hit", rhythm.Time(intent, i), Column: col, SourcePatternId: intent.Id));
            i++;
        }

        return result;
    }

    private static List<ConcreteObject> generateStream(PatternIntent intent, ManiaPatternParameters parameters, RhythmTimeline rhythm, Random rng)
        => generateSingle(intent, parameters, rhythm, rng);

    private static List<ConcreteObject> generateBurst(PatternIntent intent, ManiaPatternParameters parameters, RhythmTimeline rhythm, Random rng)
    {
        int count = Math.Clamp(parameters.Count, 2, 12);
        var order = parameters.ColumnOrder;
        var result = new List<ConcreteObject>();
        for (int i = 0; i < count; i++)
        {
            int col = order[i % order.Count];
            result.Add(new ConcreteObject($"n{result.Count + 1}", "hit", rhythm.Time(intent, i), Column: col, SourcePatternId: intent.Id));
        }

        return result;
    }

    private static List<ConcreteObject> generateJack(PatternIntent intent, ManiaPatternParameters parameters, RhythmTimeline rhythm, Random rng)
    {
        int count = Math.Clamp(parameters.Count, 2, 12);
        int col = parameters.JackColumn ?? 0;
        var result = new List<ConcreteObject>();
        for (int i = 0; i < count; i++)
        {
            result.Add(new ConcreteObject($"n{result.Count + 1}", "hit", rhythm.Time(intent, i), Column: col, SourcePatternId: intent.Id));
        }

        return result;
    }

    private static List<ConcreteObject> generateJump(PatternIntent intent, ManiaPatternParameters parameters, RhythmTimeline rhythm, Random rng)
    {
        int size = Math.Clamp(parameters.ChordSize, 1, KeyCount);
        var order = parameters.ColumnOrder;
        var result = new List<ConcreteObject>();
        int i = 0;
        while (rhythm.Time(intent, i) <= intent.EndTime)
        {
            if (!rhythm.Keep(i))
            {
                i++;
                continue;
            }

            foreach (int col in pickChord(order, size, i, rng))
                result.Add(new ConcreteObject($"n{result.Count + 1}", "hit", rhythm.Time(intent, i), Column: col, SourcePatternId: intent.Id));
            i++;
        }

        return result;
    }

    private static List<ConcreteObject> generateJumpstream(PatternIntent intent, ManiaPatternParameters parameters, RhythmTimeline rhythm, Random rng)
    {
        double density = parameters.ChordDensity;
        int size = Math.Clamp(parameters.ChordSize, 1, KeyCount);
        var order = parameters.ColumnOrder;
        var result = new List<ConcreteObject>();
        int i = 0;
        while (rhythm.Time(intent, i) <= intent.EndTime)
        {
            if (!rhythm.Keep(i))
            {
                i++;
                continue;
            }

            bool chord = rng.NextDouble() < density;
            if (chord)
            {
                foreach (int col in pickChord(order, size, i, rng))
                    result.Add(new ConcreteObject($"n{result.Count + 1}", "hit", rhythm.Time(intent, i), Column: col, SourcePatternId: intent.Id));
            }
            else
            {
                int col = order[i % order.Count];
                result.Add(new ConcreteObject($"n{result.Count + 1}", "hit", rhythm.Time(intent, i), Column: col, SourcePatternId: intent.Id));
            }

            i++;
        }

        return result;
    }

    private static List<ConcreteObject> generateSingleLn(PatternIntent intent, ManiaPatternParameters parameters, RhythmTimeline rhythm, Random rng)
    {
        double durationMs = lnDurationMs(intent, parameters, rhythm);
        var order = parameters.ColumnOrder;
        var result = new List<ConcreteObject>();
        int i = 0;
        while (rhythm.Time(intent, i) <= intent.EndTime)
        {
            if (!rhythm.Keep(i))
            {
                i++;
                continue;
            }

            int col = order[i % order.Count];
            int t = rhythm.Time(intent, i);
            result.Add(new ConcreteObject($"n{result.Count + 1}", "hold", t, EndTime: Math.Min(t + (int)Math.Round(durationMs), intent.EndTime), Column: col, SourcePatternId: intent.Id));
            i++;
        }

        return result;
    }

    private static List<ConcreteObject> generateLnRice(PatternIntent intent, ManiaPatternParameters parameters, RhythmTimeline rhythm, Random rng)
    {
        double durationMs = lnDurationMs(intent, parameters, rhythm);
        double ratio = parameters.LnRatio;
        var order = parameters.ColumnOrder;
        var result = new List<ConcreteObject>();
        int i = 0;
        while (rhythm.Time(intent, i) <= intent.EndTime)
        {
            if (!rhythm.Keep(i))
            {
                i++;
                continue;
            }

            bool isLn = rng.NextDouble() < ratio;
            int t = rhythm.Time(intent, i);
            if (isLn)
            {
                int col = order[i % order.Count];
                result.Add(new ConcreteObject($"n{result.Count + 1}", "hold", t, EndTime: Math.Min(t + (int)Math.Round(durationMs), intent.EndTime), Column: col, SourcePatternId: intent.Id));
            }
            else
            {
                int col = order[(i + 1) % order.Count];
                result.Add(new ConcreteObject($"n{result.Count + 1}", "hit", t, Column: col, SourcePatternId: intent.Id));
            }

            i++;
        }

        return result;
    }

    private static List<ConcreteObject> generateLnRelease(PatternIntent intent, ManiaPatternParameters parameters, RhythmTimeline rhythm, Random rng)
    {
        double durationMs = lnDurationMs(intent, parameters, rhythm);
        var order = parameters.ColumnOrder;
        var result = new List<ConcreteObject>();
        int i = 0;
        while (rhythm.Time(intent, i) <= intent.EndTime)
        {
            if (!rhythm.Keep(i))
            {
                i++;
                continue;
            }

            int col = order[i % order.Count];
            // release pattern: LN 结束时间精确落在后续节奏点上（不静默移动）。
            int t = rhythm.Time(intent, i);
            result.Add(new ConcreteObject($"n{result.Count + 1}", "hold", t, EndTime: Math.Min(t + (int)Math.Round(durationMs), intent.EndTime), Column: col, SourcePatternId: intent.Id));
            i++;
        }

        return result;
    }

    /// <summary>
    /// LN 时长：clamp 到 ≤1 拍（= LN family 步长，ADR-MVP-A-007 不变式）——
    /// 同列 LN 首尾相接不重叠；`ln_duration_beats &gt; 1` 时截断而非生成非法重叠。
    /// </summary>
    private static double lnDurationMs(PatternIntent intent, ManiaPatternParameters parameters, RhythmTimeline rhythm)
        => Math.Min(Math.Max(parameters.LnDurationBeats, 0.25), 1.0) * rhythm.BeatMs;

    // ---- column helpers -------------------------------------------------

    private static int[] pickChord(IReadOnlyList<int> order, int size, int index, Random rng)
    {
        size = Math.Clamp(size, 2, KeyCount);
        if (size >= KeyCount)
            return new[] { 0, 1, 2, 3 };

        // 确定性 + 多样性：从 column_order 排列中连续取 size 个（带环绕），
        // 起始偏移由 index 轮换 + rng 决定——和弦形状取决于配置的列顺序（相邻或分离均可）。
        int start = (index + rng.Next(KeyCount - size + 1)) % (KeyCount - size + 1);
        var cols = new List<int>();
        for (int k = 0; k < size; k++)
            cols.Add(order[(start + k) % order.Count]);
        return cols.Distinct().ToArray();
    }
}
