using osu.Game.Rulesets.Taiko.Objects;

namespace osu.Game.Rulesets.AiStudio.Taiko.Analysis;

/// <summary>
/// Taiko ONNX 分类器存根（M6 可选增强）。
/// 当前为无原生依赖的启发式回退；未来可替换为 Microsoft.ML.OnnxRuntime 进程内推理
/// （模型由 tools/analysis/taiko_classifier.py 离线训练并导出 ONNX）。
/// 不引入 Microsoft.ML.OnnxRuntime 包依赖，保持离线/CI 零配置可构建。
/// </summary>
public sealed class TaikoOnnxClassifier
{
    private readonly bool useOnnx;

    public TaikoOnnxClassifier(bool useOnnx = false)
    {
        this.useOnnx = useOnnx;
    }

    /// <summary>
    /// 按启发式或 ONNX 模型区分 don/kat。
    /// 当前 ONNX 路径未接入时回退为确定性规则（与生成器一致：i%4 模式的外置可测版本）。
    /// </summary>
    public HitType Classify(int index, float[]? audioWindow = null)
    {
        if (useOnnx && audioWindow != null && audioWindow.Length > 0)
        {
            float lowEnergy = 0, highEnergy = 0;
            int mid = audioWindow.Length / 4;
            for (int i = 0; i < audioWindow.Length; i++)
            {
                float e = audioWindow[i] * audioWindow[i];
                if (i < mid) lowEnergy += e;
                else highEnergy += e;
            }
            return lowEnergy >= highEnergy ? HitType.Centre : HitType.Rim;
        }

        return index % 4 == 1 ? HitType.Rim : HitType.Centre;
    }

    public bool IsOnnxAvailable => false;
}
