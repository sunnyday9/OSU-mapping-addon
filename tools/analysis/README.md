# tools/analysis — Ranked 语料离线分析（M2 占位 / M3 实现）

本目录为 **离线 Python** 工具链，不进入游戏运行链路；由 `.github/workflows/ai-tools.yml`（ruff + pytest）与 `corpus-refresh.yml`（月度语料刷新）驱动（`docs/PLAN.md §4` / `docs/architecture.md §2.1`）。

## 目标（PLAN.md §3 G4）

离线采集一批真实 ranked 谱面（osu! API，仅统计参数不随包分发），按 BPM 分箱拟合：

- 相邻物件间距均值/方差
- stream 长度
- 滑条:圈比例
- combo 长度

等分布；生成谱面的对应参数必须落在 **P5–P95** 区间（防“机器人感”与极端值，与 `QualityGateRunner` G4 联动）。

## 现状（M2 占位）

- `corpus.py`：`load_ranked_corpus` / `fit_distributions` 仅返回与 `QualityGateRunner` G4/G3 v1 代理一致的占位区间（`spacing_px [30, 400]`、`slider_ratio [0.15, 0.85]`、`grid_ratio [0.95, 1.0]`），真实拟合在 M3 以语料统计替换（`rc-coverage.md §4` D-04）。
- `test_placeholder.py`：占位 pytest，供 CI 有可执行目标。
- `requirements.txt`：`ruff` / `pytest`（与 `ai-tools.yml` 一致）。

## 如何运行

```bash
pip install -r tools/analysis/requirements.txt
ruff check tools/analysis
pytest tools/analysis -v
python -c "from tools.analysis.corpus import fit_distributions; print(fit_distributions())"
```

## 后续（M3）

- 接入 osu! API 语料拉取与缓存（`corpus-refresh.yml` 自动开 PR 刷新参数表）；
- 将拟合结果以 JSON/CSV 产出并由 `QualityGateRunner` 读取，替换当前 `const` 阈值。
