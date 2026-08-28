using global::AiStudio.Core.Analysis;
using global::AiStudio.Core.MappingIr;
using global::AiStudio.Core.MappingIr.Backends;
using global::AiStudio.Core.MappingIr.Calibration;
using global::AiStudio.Core.MappingIr.Candidates;
using global::AiStudio.Core.MappingIr.Critique;
using global::AiStudio.Core.MappingIr.Difficulty;
using global::AiStudio.Core.MappingIr.Evidence;
using global::AiStudio.Core.MappingIr.GlobalPlanning;
using global::AiStudio.Core.MappingIr.LocalPlanning;
using global::AiStudio.Core.MappingIr.Model;
using global::AiStudio.Core.MappingIr.Rendering;

namespace osu.Game.Rulesets.AiStudio.Mania.Synthesis;

/// <summary>
/// MVP-B 门面：Mapping IR 管线 + 官方 SR 校准闭环。
/// 以 DensityScale 为旋钮迭代重跑管线，直到官方 ManiaDifficultyCalculator
/// 实测 SR 落在 <see cref="DifficultyProfile.TargetStarRating"/> ± Tolerance 内。
/// 确定性：同 seed 同输入 → 同输出（含校准后的文档）。
/// </summary>
public sealed class ManiaIrCalibratedPipeline
{
    private readonly IAudioAnalyzer analyzer;
    private readonly IDifficultyEvaluator difficultyEvaluator;
    private readonly StarRatingCalibrationLoop calibrationLoop;

    public ManiaIrCalibratedPipeline(
        IAudioAnalyzer? analyzer = null,
        IDifficultyEvaluator? difficultyEvaluator = null,
        StarRatingCalibrationLoop? calibrationLoop = null)
    {
        this.analyzer = analyzer ?? new global::AiStudio.Core.MappingIr.Analysis.SyntheticAudioAnalyzer(180.0, 60000);
        this.difficultyEvaluator = difficultyEvaluator ?? new ManiaOfficialDifficultyEvaluator();
        this.calibrationLoop = calibrationLoop ?? new StarRatingCalibrationLoop();
    }

    /// <summary>
    /// 运行管线并校准 SR，返回含收敛元数据的完整结果。
    /// </summary>
    public CalibrationResult RunCalibrated(string audioPath, DifficultyProfile difficultyProfile, int seed = 0, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(audioPath);
        ArgumentNullException.ThrowIfNull(difficultyProfile);

        return calibrationLoop.Calibrate(
            difficultyProfile,
            scale => BuildPipeline(scale).Run(audioPath, difficultyProfile, seed, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// 运行管线并校准 SR，返回校准后的最终文档（Evaluation 含 observed_star_rating 与 DifficultyKnown=true）。
    /// 需要收敛状态（迭代次数/是否达标）时用 <see cref="RunCalibrated"/>。
    /// </summary>
    public MappingDocument Run(string audioPath, DifficultyProfile difficultyProfile, int seed = 0, CancellationToken cancellationToken = default)
        => RunCalibrated(audioPath, difficultyProfile, seed, cancellationToken).Document;

    /// <summary>
    /// 渲染 MappingDocument 为完整 .osu 文本。与官方评估器（<see cref="ManiaOfficialDifficultyEvaluator"/>）
    /// 使用同一渲染器，保证「报告 SR = 实测 SR」（CONTEXT.md 不变量）。
    /// </summary>
    public string RenderOsu(MappingDocument document)
        => new ManiaOsuRenderer().Render(document);

    private MappingIrPipeline BuildPipeline(double densityScale)
        => new(
            analyzer,
            new DeterministicEvidenceBuilder(),
            new DeterministicGlobalPlanner(),
            new DeterministicLocalPlanner(),
            new DeterministicCandidateGenerator { DensityScale = densityScale },
            new DeterministicCandidateRanker(),
            new Mania4KMappingBackend(),
            new BaselineMappingCritic(),
            difficultyEvaluator);
}
