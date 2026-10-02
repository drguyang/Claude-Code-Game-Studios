# Story 001: 技能注册表与技能定义

> **Epic**: 技能与熟练度
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28
## Context

**GDD**: `design/gdd/skill-system.md`(§3.1 技能清单 · §3.3 死亡惩罚 · §4.1 BASE 默认值 · §7 调参旋钮)
**Requirement**: TR-skill-006(19 项技能的依赖与解锁关系) · TR-skill-005(运行时调参表的默认值须定点化)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-026(主): 技能成长的定点化与持久化契约
**ADR Decision Summary**: 技能门槛/解锁/档位判定走整数等级比较,不经浮点;Fix 字段 JSON 写字符串→FixParse,int 计数写整数;构建期烘焙由 ADR-014 两阶段管线完成。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数数据结构,不触及任何 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 技能定义全部整数域(等级 int / Fix 字段 Fix 类型 / 枚举 int);Fix 字段 JSON 写字符串→FixParse
- Forbidden: float/double 出现在技能注册表或调参表;Build / Skill / Nov 等 Unity 保留字段名(承 ADR-025 清单封闭性)
- Guardrail: SKILL_CAP = 60 是硬上限,所有技能共用同一上限

---

## Acceptance Criteria

*From GDD `design/gdd/skill-system.md`, scoped to this story:*

- [ ] 19 项技能全部可枚举、可查询,每项有名称/类别/成长触发/BASE/依赖列表
- [ ] P0 七项技能(诊断/急救/处方用药/采集/炮制/徒手/短兵)与 P1a 十二项可区分
- [ ] 技能间依赖关系可声明为「前置技能 ≥ N 级才解锁当前」
- [ ] `SKILL_CAP = 60` 作为单一常量生效,所有技能共用
- [ ] 调参表数据结构可承载: C / P / BASE[skill] / NOVELTY_FIRST / NOVELTY_DECAY / NOVELTY_COOLDOWN / DEATH_LOSS / MED_COMBAT_MOD / WeaponMultiplier[line] / DIAG_TIERS / INSIGHT_TIERS
- [ ] Fix 字段在 JSON 源码中写字符串形式(`"19/20"`),构建期经 FixParse 转换
- [ ] `DIAG_TIERS`(10/20/35/50)可查询,`35` 标记为 P1a 检验线(不参与槽划分)

---

## Implementation Notes

*Derived from ADR-026 Implementation Guidelines:*

1. 技能注册表 = 纯数据结构(无 Unity 依赖),住 `Sim` 程序集(或 `Sim.Contracts`,视 ADR-025 清单而定 —— **只读契约侧**)。
2. `SkillId` = `int` 枚举或索引,不承载 `Fix`。
3. `WeaponMultiplier` 是 `Fix`(承 ADR-006);`MED_COMBAT_MOD` 也是 `Fix` —— 这两个字段 JSON 写字符串。
4. `DEATH_LOSS`(`0.95`)、`NOVELTY_FIRST`(`3.0`)、`NOVELTY_DECAY`(`0.2`) 均为 `Fix` 字段,JSON 写 `"19/20"` / `"3/1"` / `"1/5"`。
5. `BASE[skill]` 在 GDD §4.1 中 19 项各不相同;其中 `奔跑 = 0.2` 是 `Fix` 字段(非 int),JSON 写 `"1/5"`。
6. `DIAG_TIERS` = `int[]`(写整数);`INSIGHT_BONUS` = `Fix[]`(写字符串),由 42 读作 UI 阈值。
7. 构建期断言:① 所有 `SkillId` 在 0..18 范围内;② P0/P1a 标记与 §3.1 表格一致(用户裁定口径)。

---

## Out of Scope

- [Story 002]: XP_gain 计算逻辑
- [Story 003]: XP_to_next 曲线与 FixPow/FixSqrt
- [Story 004]: CombatPower 与医术修正
- [Story 005]: 死亡掉级逻辑
- [Story 006]: 新颖度追踪字典
- [Story 007]: SkillGrown 事件发射
- [Story 008]: 从流重构与持久化 round-trip

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/SkillSystem/skill_registry_test.cs` — must exist and pass
**Status**: [x] Created 2026-09-28 — 23 tests, all passing

---

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 23/23 passing
**Deviations**: None
**Test Evidence**: Logic: `unity/Assets/Tests/EditMode/SkillSystem/skill_registry_test.cs` — 23 tests, all passing
**Code Review**: Unity-specialist APPROVED + qa-tester reviewed
