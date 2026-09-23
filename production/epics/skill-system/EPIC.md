# Epic: 技能与熟练度

> **Layer**: Foundation
> **GDD**: design/gdd/skill-system.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories skill-system`

## Overview

技能与熟练度(30)让「见识」成为一种真实的游戏资源:本作没有天赋树、没有职业、没有等级,玩家的一切能力由 **19 项技能**表示,每项技能按「做了就涨」成长,成长速度受**新颖度**调节 —— 反复处理同样的病人几乎不涨,第一次遇到新东西才大涨。上限 60;玩家死亡每项损失 5%(向下取整),叙事上是「手生」而非遗忘。玩家侧幻想是「我不是变强了,我是见过得多了」。架构上 30 是**全案唯一以 `QueryLevel` / `EmitGrowth` 两接口对外**的 sim 模块:`XP_to_next(n) = C × n^P`(指数 ∈ {整数, 整数+1/2},旧例值 1.4 作废)、`CombatPower = (CombatSkillLevel × WeaponMultiplier) × (1 + 医术修正)` 等三条公式全程整数定点域求值(Q16.16,零 float),`SkillGrown` 落病史流、不独立快照、从流重构。承重 ADR-026 于 2026-09-23 兑现 Required #4,八条 TR 全 covered。逐条数值(P 终值、冷却 tick 数、调参表)归用户数值轮,机制值域冻结。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-026: 技能成长的定点化与持久化契约 | FixPow 唯一整数幂实现(指数 ∈ {整数, 整数+1/2},FixSqrt = 整数牛顿,禁 libm/float);CombatPower 定点求值 + Fix 传入 25;SkillGrown 落病史流不独立快照;折叠豁免扩一类 Kind(结清 OQ-7a-9);义务 14 入 ADR-010 §三 | LOW |
| ADR-005: 确定性模拟与状态同步模型 | 全部模拟数学在整数定点域;病史事件流是唯一真源;主机唯一执行 Step / CatchUp;五个 P0 抽象点 | HIGH |
| ADR-006: 定点域边界数据契约 | FixParse 为外部数据→Fix 唯一入口(拒浮点字面量);单一舍入 ROUND_HALF_AWAY_FROM_ZERO;Fix 不可经 Unity 内置序列化器 | MEDIUM |
| ADR-009: 世界状态事件化边界 | 三流全序与域归属表;SkillGrown 属病史流(第二十七批具名入 entities.yaml,V-2 条目 33→34) | MEDIUM |
| ADR-010: 7a 持久化与存档格式 | 全二进制 codec;义务汇总表 §三单一出处(义务行 14 = 技能成长持久化);病史流序列化与折叠 | MEDIUM |
| ADR-014: 数据管线与 JSON 解析器 | 作者态 assets/data/*.json → 构建期烘 *.cooked;Fix 字段 JSON 写字符串 → FixParse;玩家构建零 JSON 解析器 | MEDIUM |
| ADR-024: 三流 Kind 单一登记真源 | 真源 = entities.yaml(禁从散文解析);构建期生成器 + 断言 A1–A5;SkillGrown 已具名入 registry | LOW |

**Engine Risk**: **HIGH**(ADR-005 最高)。ADR-026 本体为纯整数算术 + BCL,LOW、零 post-cutoff API;但其验证义务挂 ADR-005 / ADR-012 的 HIGH 域实测前置 —— `FixPow` 整数实现须过跨平台逐位一致(IL2CPP vs Mono,归黄金夹具矩阵),`Fix.Div` 与 `FixParse` 舍入一致性归 EditMode 探针。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-skill-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-23 实测):**8 条 = 8 covered + 0 partial + 0 gap + 0 no-adr-by-design**;
> **GDD Requirements Covered by ADRs = 8 / 8**;**Untraced = None**。
> 8 条全部于 2026-09-23 Required ADR #4 兑现轮翻 ✅(`ADR-026`,含原 Foundation 唯一裸缺口
> `TR-skill-002` 定点域与 `TR-skill-008` 存档持久化)。数值(P 终值 / `NOVELTY_COOLDOWN` tick 数 /
> 调参表默认值)归用户数值轮 —— 机制值域冻结,epic 不因此阻塞。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-skill-001 | XP_to_next(n) = C × n^P(指数 ∈ {整数, 整数 + 1/2};旧例值 1.4 作废) | ADR-026 ✅ |
| TR-skill-002 | 熟练度成长在整数定点域内求值(Q16.16,sim 零 float) | ADR-026 ✅ |
| TR-skill-003 | CombatPower = (CombatSkillLevel × WeaponMultiplier) × (1 + 医术修正) | ADR-026 ✅ |
| TR-skill-004 | 医术修正 = 0.20 × (关联医术等级 / 60) | ADR-026 ✅ |
| TR-skill-005 | 运行时调参表的默认值须定点化(Fix 字段 JSON 写字符串) | ADR-026 ✅ |
| TR-skill-006 | 19 项技能的依赖与解锁关系(整数等级比较) | ADR-026 ✅ |
| TR-skill-007 | 30 与 25 格斗线的数据边界(CombatPower 单一交接点) | ADR-026 ✅ |
| TR-skill-008 | 技能成长的存档持久化(SkillGrown 落病史流、不独立快照、从流重构) | ADR-026 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/skill-system.md` are verified
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

Run `/create-stories skill-system` to break this epic into implementable stories.
