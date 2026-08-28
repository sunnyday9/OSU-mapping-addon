using System.IO;
using System.Text;
using AiStudio.Core.Analysis;
using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;

namespace osu.Game.Rulesets.AiStudio.Mania.Synthesis;

/// <summary>
/// Mania 生成器（Mapping IR 管线版，ADR-011）：IMapGenerator 的插件侧产品环——
/// 编辑器设置 → 目标难度档案（最小映射，balanced 维度）→ 已校准 IR 管线（默认注入 BASS 分析器）→
/// 渲染写盘 + 音频复制 → GenerationResult（校准元数据进 QualityReport）。
/// 不抛出契约：缺音频 / 分析失败等折入 Success=false，失败不落盘；Core 保持纯内存库。
/// 报告 SR = 实测 SR（官方 ManiaDifficultyCalculator，CONTEXT.md 不变量）。
/// </summary>
public sealed class ManiaIrMapGenerator : IMapGenerator
{
    private readonly ManiaIrCalibratedPipeline pipeline;

    public ManiaIrMapGenerator(IAudioAnalyzer? analyzer = null)
        => pipeline = new ManiaIrCalibratedPipeline(analyzer ?? new Analysis.BassAudioAnalyzer());

    public async Task<GenerationResult> GenerateAsync(GenerationSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(settings.AudioPath) || !File.Exists(settings.AudioPath))
            return fail($"Audio file not found: {settings.AudioPath}");

        string? osuPath = null;
        string? copiedAudio = null;
        bool outputDirExisted = false;
        string outputDirectory = string.Empty;

        try
        {
            var profile = MapProfile(settings);
            var calibrated = pipeline.RunCalibrated(settings.AudioPath, profile, seed: 0, cancellationToken);
            var document = calibrated.Document;

            // 渲染先于任何磁盘 I/O：渲染失败不得留下半成品。
            string osuText = pipeline.RenderOsu(document);

            outputDirectory = resolveOutputDirectory(settings);
            outputDirExisted = Directory.Exists(outputDirectory);
            Directory.CreateDirectory(outputDirectory);

            string audioFileName = Path.GetFileName(settings.AudioPath);
            string audioOutputPath = copyAudio(settings.AudioPath, outputDirectory, audioFileName, out string? note, out bool deletableAudio);
            if (deletableAudio)
                copiedAudio = audioOutputPath;

            osuPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(audioFileName)}_mania.osu");
            await File.WriteAllTextAsync(osuPath, osuText, new UTF8Encoding(false), cancellationToken);

            double bpm = document.MusicTimeline.Tempo.BaseBpm;
            int objectCount = document.ConcreteObjects?.Count ?? 0;

            // 元数据字段名与 spec #16 验收口径一致（converged/iterations/observed_sr/final_density_scale）。
            string detail = $"Mania IR generated at {bpm:0.##} BPM, {objectCount} objects (4K)."
                + (calibrated.ObservedSr is { } sr ? $" observed_sr={sr:0.00}." : " official SR evaluator unavailable.")
                + $" Calibration: converged={calibrated.Converged}, iterations={calibrated.Iterations}, final_density_scale={calibrated.FinalDensityScale:0.###}.";
            if (note != null) detail += " " + note;

            var report = new QualityGateReport
            {
                Gates = new[]
                {
                    new QualityGateResult { Name = "Mania generation", Status = GateStatus.Passed, Detail = detail },
                },
            };

            return new GenerationResult
            {
                Success = true,
                QualityReport = report,
                OutputFilePath = osuPath,
                AudioOutputPath = audioOutputPath,
            };
        }
        catch (OperationCanceledException)
        {
            // 取消路径无结果对象可携带说明，尽力清理即可。
            describeCleanupFailures(osuPath, copiedAudio, outputDirectory, outputDirExisted);
            throw;
        }
        catch (Exception ex)
        {
            // 接缝契约（IMapGenerator）：Success=false 时不得落盘——清理已产生的任何文件；
            // 清理自身失败必须可见，不得静默吞掉契约违约。
            string cleanupNote = describeCleanupFailures(osuPath, copiedAudio, outputDirectory, outputDirExisted);
            return fail($"Mania IR generation failed: {ex.Message}{cleanupNote}");
        }
    }

    /// <summary>尽力清理失败路径已落盘的产物；返回失败说明（空串 = 全部清理成功）。</summary>
    private static string describeCleanupFailures(string? osuPath, string? copiedAudio, string outputDirectory, bool outputDirExisted)
    {
        var failures = new List<string>();
        try { if (osuPath != null) File.Delete(osuPath); } catch (Exception ex) { failures.Add($"osu file {osuPath} ({ex.Message})"); }
        try { if (copiedAudio != null) File.Delete(copiedAudio); } catch (Exception ex) { failures.Add($"audio copy {copiedAudio} ({ex.Message})"); }
        try
        {
            if (!outputDirExisted && Directory.Exists(outputDirectory) && !Directory.EnumerateFileSystemEntries(outputDirectory).Any())
                Directory.Delete(outputDirectory);
        }
        catch (Exception ex) { failures.Add($"output directory {outputDirectory} ({ex.Message})"); }

        return failures.Count > 0 ? $" Cleanup failed for: {string.Join("; ", failures)}." : string.Empty;
    }

    /// <summary>MVP-B 校准闭环验证所用维度档案（ManiaIrCalibrationTest 验证 5.5 ± 0.15 的同款数值；
    /// 注意区别于 Core 的 <see cref="DifficultyProfile.Balanced"/> 默认值——后者无验证背书且当前无生产调用方）。</summary>
    public static readonly DimensionProfile CalibrationVerifiedDimensions = new(0.72, 0.64, 0.55, 0.48, 0.42, 0.20, 0.30);

    /// <summary>
    /// 编辑器设置 → 目标难度档案（grill 决策 Q4 最小映射）：目标 SR / 容差直传，
    /// 维度用 <see cref="CalibrationVerifiedDimensions"/>；
    /// TargetLevel 有意不映射（IR 渲染器难度值固定，ADR-011 已知缺口）。
    /// </summary>
    public static DifficultyProfile MapProfile(GenerationSettings settings)
        => new(
            settings.TargetStarRating,
            CalibrationVerifiedDimensions,
            new DifficultyPreferences(AllowExtremePatterns: false, PreferReadability: true, PreferMusicSync: true, PreferPatternVariety: true),
            settings.StarRatingTolerance);

    private static string resolveOutputDirectory(GenerationSettings settings) => string.IsNullOrWhiteSpace(settings.OutputDirectory)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "osu-ai-studio-output")
        : settings.OutputDirectory;

    private static string copyAudio(string audioPath, string outputDirectory, string audioFileName, out string? note, out bool deletable)
    {
        string audioOutputPath = Path.Combine(outputDirectory, audioFileName);
        note = null;
        deletable = false;
        try
        {
            bool existedBefore = File.Exists(audioOutputPath);
            if (!string.Equals(Path.GetFullPath(audioPath), Path.GetFullPath(audioOutputPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(audioPath, audioOutputPath, overwrite: true);
                // 仅当目标原本不存在时才允许失败清理删除——不销毁用户既有文件（覆盖本身沿用 M2 产品约定）。
                deletable = !existedBefore;
            }
        }
        catch (Exception ex)
        {
            note = $"Audio copy failed ({ex.Message}), falling back to original path.";
            audioOutputPath = audioPath;
        }
        return audioOutputPath;
    }

    private static GenerationResult fail(string message)
        => new GenerationResult { Success = false, ErrorMessage = message };
}
