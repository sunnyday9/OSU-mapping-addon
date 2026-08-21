# ADR-009 IDistributionProvider 与语料凭据安全

- 日期：2026-08-21（M3）
- 状态：已采纳

## 背景
PLAN §3 门禁 G4 需以 ranked 语料的 P5–P95 约束“像人做的图”；语料采集涉及 `OSU_API_CLIENT_ID/SECRET`。

## 决策
引入 `IDistributionProvider` 抽象，`QualityGateRunner` 注入 `FileDistributionProvider` 读取 `tools/analysis/distributions.json`，缺省回退 `DistributionSet.Default`；`corpus.py` 仅从环境变量读取凭据，无凭据时合成回退，保证 CI 不依赖密钥；`corpus-refresh.yml` 以相同约束自动刷新并开 PR 需人工复核。

## 后果
- 正：G4 可接真实分布亦可在离线/CI 无密钥环境稳定通过；凭据不落地源码/示例/测试。
- 负：合成回退的 P5–P95 为宽松占位，需真实语料刷新后收紧。

## 取舍
以可回退抽象换可用性，以人工复核 PR 换分布可信度。
