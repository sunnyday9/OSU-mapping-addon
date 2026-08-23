using AiStudio.Core.MappingIr;
using AiStudio.Core.MappingIr.Analysis;
using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Serialization;

// MVP A 端到端演示：
// 合成音频（确定性分析器）→ MusicTimeline → MappingPlan → Mania4K Pattern 生成 → 校验 → .osu 渲染 → JSON 文档导出。
// 用法: mapping-ir-demo [输出目录] [seed]

string outRoot = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "aistudio-mvp-a-" + Guid.NewGuid().ToString("N")[..6]);
int seed = args.Length > 1 ? int.Parse(args[1]) : 42;
Directory.CreateDirectory(outRoot);

// 1. 三段式合成歌曲：intro (0-20s, low energy) → chorus (20-40s, high energy) → outro (40-60s, low energy)
var analyzer = new SyntheticAudioAnalyzer(
    bpm: 174.0,
    durationMs: 60000,
    sectionStarts: new[] { 0.0, 20000.0, 40000.0 },
    sectionEnergies: new[] { 0.35, 0.85, 0.30 });

var pipeline = new MappingIrPipeline(analyzer);

// 2. 目标：mania 4K，5.5★，偏高密度/LN 适中
var profile = new DifficultyProfile(
    TargetStarRating: 5.5,
    Dimensions: new DimensionProfile(
        Density: 0.72,
        RhythmComplexity: 0.64,
        Reading: 0.55,
        Stamina: 0.48,
        Technicality: 0.42,
        Movement: 0.20,
        LnComplexity: 0.30),
    Preferences: new DifficultyPreferences(AllowExtremePatterns: false, PreferReadability: true, PreferMusicSync: true, PreferPatternVariety: true),
    Tolerance: 0.15);

string pseudoAudio = Path.Combine(outRoot, "synthetic_174bpm.mp3");
File.WriteAllText(pseudoAudio, "synthetic placeholder");

Console.WriteLine($"[mvp-a] pipeline start seed={seed}");
var document = pipeline.Run(pseudoAudio, profile, seed);
Console.WriteLine($"[mvp-a] sections={document.MusicTimeline.Sections.Count} intents={document.MappingPlan.Intents.Count} patterns={document.MappingPlan.Patterns.Count} objects={document.ConcreteObjects?.Count}");
Console.WriteLine($"[mvp-a] validation valid={document.Evaluation.Valid} issues={document.Evaluation.Issues?.Count}");
Console.WriteLine($"[mvp-a] music alignment={document.Evaluation.MusicAlignmentScore:0.000}");

// 3. .osu 渲染
string osuPath = Path.Combine(outRoot, "ai_generated_4k.osu");
string osu = pipeline.RenderOsu(document);
File.WriteAllText(osuPath, osu);
Console.WriteLine($"[mvp-a] osu written: {osuPath} ({osu.Length} bytes)");

// 4. JSON 文档
string jsonPath = Path.Combine(outRoot, "mapping_ir.json");
string json = JsonMappingIrSerializer.Serialize(document);
File.WriteAllText(jsonPath, json);
Console.WriteLine($"[mvp-a] ir json written: {jsonPath} ({json.Length} bytes)");

// 5. 确定性验证：同 seed 重跑应完全一致
var document2 = pipeline.Run(pseudoAudio, profile, seed);
string json2 = JsonMappingIrSerializer.Serialize(document2);
Console.WriteLine($"[mvp-a] deterministic={json == json2}");

// 6. 摘要输出
var summary = new
{
    bpm = document.MusicTimeline.Tempo.BaseBpm,
    duration_ms = document.MusicTimeline.DurationMs,
    sections = document.MusicTimeline.Sections.Select(s => $"{s.Type}:{s.StartTime}-{s.EndTime}@{s.Energy:0.00}").ToArray(),
    patterns = document.MappingPlan.Patterns.Select(p => $"{p.Family}:{p.StartTime}-{p.EndTime}").ToArray(),
    objects = document.ConcreteObjects?.Count ?? 0,
    valid = document.Evaluation.Valid,
    alignment = document.Evaluation.MusicAlignmentScore,
};
Console.WriteLine($"[mvp-a] SUMMARY {System.Text.Json.JsonSerializer.Serialize(summary)}");
Console.WriteLine($"OUTDIR={outRoot}");
