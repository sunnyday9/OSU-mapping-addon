# ADR-001 继承 Ruleset 基类而非官方模式 Ruleset

- 日期：2026-08-15（M0）
- 状态：已采纳

## 背景
官方四个模式 ruleset（OsuRuleset/TaikoRuleset/CatchRuleset/ManiaRuleset）均实现 `ILegacyRuleset`，其 `LegacyID`（0/1/2/3）为非 virtual。第三方若直接继承这些类，会带着冲突的 LegacyID 进入 legacy 注册分支，与内置 ruleset 撞车后被静默跳过——插件不会出现在游戏内且无任何报错。

## 决策
四个模式的插件一律继承 `Ruleset` 基类，通过 NuGet `ppy.osu.Game` + `ppy.osu.Game.Rulesets.*` 复用官方公开组件（converter/processor/difficulty/performance/verifier/mods），在 ruleset 内部委托官方实例而非继承。

## 后果
- 正：规避静默不注册陷阱，符合社区主流做法（sentakki 同款），每个程序集只承担一个 public Ruleset 子类，加载器可正确识别。
- 负：需自行转发全部 `Ruleset` 工厂方法，新增官方接口时需跟随补齐；已由 `api-compat.yml` 探针对抗。

## 取舍
放弃“少写转发代码”的便利，换取“可被加载”的正确性；该取舍不可逆，M0 验收即验证可见性。
