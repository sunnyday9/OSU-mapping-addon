"""
Taiko don/kat 分类器训练桩（M6 可选增强，离线 Python）。

当前为离线训练说明与导出桩，不引入运行时依赖：
- 输入：ranked taiko 谱面的音频窗与标注（don=Rim/Centre 之外的 Rim/Centre 区分）
- 模型：小型 1D-CNN 或频带能量阈值基线（低频能量高→don，高频→kat）
- 导出：torch.onnx.export → taiko_classifier.onnx，供 TaikoOnnxClassifier 未来接入 Microsoft.ML.OnnxRuntime

凭据约束：不读取、不写入任何密钥；仅处理本地音频/标注。

离线用法：
    python tools/analysis/taiko_classifier.py --help
    python tools/analysis/taiko_classifier.py --train data/taiko_wavs --out taiko_classifier.onnx  # 需自备数据与 torch/onnx

当前未接入推理链路时，Taiko 生成器使用 TaikoOnnxClassifier 的启发式回退（低频段能量 vs 高频段能量，
或确定性 i%4 模式），保证零额外依赖可构建/可测试。

参考：https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!taiko
"""

from __future__ import annotations

import argparse


def train_placeholder(data_dir: str | None = None, out_path: str = "taiko_classifier.onnx") -> None:
    print("== Taiko classifier training placeholder ==")
    if data_dir:
        print(f"  data_dir: {data_dir}")
    print(f"  out: {out_path}")
    print("  说明：本桩仅作离线训练流程占位，当前未实现真实训练。")
    print("  真实训练需：local wav + 标注 + torch + onnx，产出 onnx 后由")
    print("  src/osu.Game.Rulesets.AiStudio.Taiko/Analysis/TaikoOnnxClassifier.cs 按需加载。")
    print("  未接入时生成器走启发式回退，不影响构建与测试。")


def main() -> None:
    parser = argparse.ArgumentParser(description="Taiko don/kat 分类器训练桩（M6 可选 ONNX）")
    parser.add_argument("--train", dest="data_dir", default=None, help="训练数据目录（可选）")
    parser.add_argument("--out", dest="out", default="taiko_classifier.onnx", help="导出 onnx 路径")
    args = parser.parse_args()

    if args.data_dir:
        train_placeholder(args.data_dir, args.out)
    else:
        parser.print_help()
        print("\n示例：python tools/analysis/taiko_classifier.py --train data/taiko --out taiko_classifier.onnx")


if __name__ == "__main__":
    main()
