"""
Ranked 语料分布拟合（M3）：离线合成样本拟合 P5–P95，输出 distributions.json。

凭据约束：仅从环境变量读取（如需 API），源码不写字面量；默认离线合成，保证 CI 无密钥可用。
"""

from __future__ import annotations

import json
import math
import pathlib


def _percentile(sorted_vals: list[float], pct: float) -> float:
    if not sorted_vals:
        return 0.0
    k = (len(sorted_vals) - 1) * pct / 100.0
    f = math.floor(k)
    c = math.ceil(k)
    if f == c:
        return sorted_vals[int(k)]
    d0 = sorted_vals[int(f)] * (c - k)
    d1 = sorted_vals[int(c)] * (k - f)
    return d0 + d1


def load_ranked_corpus(path: str | None = None) -> list[dict]:
    if path and pathlib.Path(path).exists():
        try:
            data = json.loads(pathlib.Path(path).read_text(encoding="utf-8"))
            if isinstance(data, list):
                return data
        except (OSError, json.JSONDecodeError):
            pass
    return _synthetic_corpus()


def _synthetic_corpus() -> list[dict]:
    samples: list[dict] = []
    for i in range(300):
        diff = 1.0 + (i % 50) * 0.1
        bpm = 90 + (i * 7) % 110
        base_spacing = 110 + diff * 10 + (bpm - 120) * 0.15
        offset = ((i * 13) % 29 - 14) * 3.0
        spacing = min(430, max(22, base_spacing + offset))
        base_slider = 0.28 + diff * 0.035
        slider_offset = ((i * 17) % 23 - 11) * 0.014
        slider_ratio = min(0.88, max(0.08, base_slider + slider_offset))
        samples.append({"spacing_px": spacing, "slider_ratio": slider_ratio, "bpm": float(bpm), "sr": diff})
    for _ in range(50):
        samples.append({"spacing_px": 45.0, "slider_ratio": 0.22, "bpm": 120.0, "sr": 2.0})
        samples.append({"spacing_px": 140.0, "slider_ratio": 0.25, "bpm": 120.0, "sr": 3.5})
    return samples


def fit_distributions(corpus: list[dict] | None = None) -> dict[str, dict[str, float]]:
    if not corpus:
        corpus = _synthetic_corpus()

    spacings = sorted(float(c.get("spacing_px", 0)) for c in corpus if "spacing_px" in c)
    sliders = sorted(float(c.get("slider_ratio", 0)) for c in corpus if "slider_ratio" in c)

    spacing_p5 = _percentile(spacings, 5) if spacings else 30.0
    spacing_p95 = _percentile(spacings, 95) if spacings else 400.0
    slider_p5 = _percentile(sliders, 5) if sliders else 0.15
    slider_p95 = _percentile(sliders, 95) if sliders else 0.85

    spacing_p5 = min(max(spacing_p5, 18), 45)
    spacing_p95 = min(max(spacing_p95, 280), 450)
    slider_p5 = min(max(slider_p5, 0.08), 0.22)
    slider_p95 = min(max(slider_p95, 0.55), 0.92)

    return {
        "spacing_px": {"p5": round(float(spacing_p5), 4), "p95": round(float(spacing_p95), 4)},
        "slider_ratio": {"p5": round(float(slider_p5), 4), "p95": round(float(slider_p95), 4)},
        "grid_ratio": {"p5": 0.95, "p95": 1.0},
    }


def main() -> None:
    dist = fit_distributions(load_ranked_corpus())
    out = pathlib.Path(__file__).with_name("distributions.json")
    out.write_text(json.dumps(dist, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Wrote {out} : {dist}")

    proj_out = pathlib.Path(__file__).parents[2] / "src" / "osu.Game.Rulesets.AiStudio.Osu" / "distributions.json"
    try:
        proj_out.write_text(json.dumps(dist, indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"Wrote {proj_out}")
    except OSError:
        pass


if __name__ == "__main__":
    main()
