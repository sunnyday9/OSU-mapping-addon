using global::AiStudio.Core.MappingIr.Calibration;
using global::AiStudio.Core.MappingIr.Candidates;
using global::AiStudio.Core.MappingIr.Model;

namespace osu.Game.Rulesets.AiStudio.Mania.Synthesis;

/// <summary>
/// MVP-B：官方 ManiaDifficultyCalculator 的 IR 文档 SR 评估器（ADR-MVP-B-002）。
/// 路径：IR 文档 → ManiaOsuRenderer 渲染 .osu → LegacyBeatmapDecoder 解码 →
/// ManiaInMemoryWorkingBeatmap → ManiaDifficultyCalculator → StarRating。
/// 纯内存、无磁盘；失败（不可解码/非有限）返回 null，保持 Evaluation.DifficultyKnown=false 语义。
/// </summary>
public sealed class ManiaOfficialDifficultyEvaluator : global::AiStudio.Core.MappingIr.Difficulty.IDifficultyEvaluator
{
    private readonly global::AiStudio.Core.MappingIr.Rendering.ManiaOsuRenderer renderer = new();

    public double? TryEvaluateStarRating(MappingDocument document)
    {
        try
        {
            string osu = renderer.Render(document);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(osu));
            using var reader = new osu.Game.IO.LineBufferedReader(stream);
            var beatmap = new osu.Game.Beatmaps.Formats.LegacyBeatmapDecoder().Decode(reader, Array.Empty<osu.Game.IO.LineBufferedReader>());

            var working = new ManiaInMemoryWorkingBeatmap(beatmap);
            double stars = new osu.Game.Rulesets.Mania.Difficulty.ManiaDifficultyCalculator(
                new osu.Game.Rulesets.Mania.ManiaRuleset().RulesetInfo, working).Calculate().StarRating;
            return double.IsFinite(stars) ? stars : null;
        }
        catch
        {
            return null;
        }
    }
}
