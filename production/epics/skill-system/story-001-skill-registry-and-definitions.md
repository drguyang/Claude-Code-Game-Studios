# Story 001: 技能注册表与技能定义

> **Epic**: 技能与熟练度
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
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

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: 19 项技能全部可枚举
  - Given: 技能注册表已加载
  - When: 枚举全部技能
  - Then: 恰好 19 项,每项有非空名称/类别/成长触发
  - Edge cases: 重复 SkillId = 构建期断言失败

- **AC-2**: P0/P1a 分流正确
  - Given: 注册表已加载
  - When: 按 P0 标志过滤
  - Then: 恰好 7 项 P0(诊断/急救/处方用药/采集/炮制/徒手/短兵),其余 12 项为 P1a
  - Edge cases: 新增 P1a 技能时不影响 P0 集合

- **AC-3**: 技能依赖解锁条件正确
  - Given: 诊断等级 = 5,辨证已注册为「诊断 ≥ 10 解锁」
  - When: 查询辨证是否可解锁
  - Then: 返回 false(5 < 10)
  - Edge cases: 等于阈值时返回 true(≥ 语义)

- **AC-4**: SKILL_CAP 单一常量
  - Given: 注册表已加载
  - When: 查询任何技能的等级上限
  - Then: 全部返回 60
  - Edge cases: 等级 60 后不能再获得经验(Story 003 覆盖)

- **AC-5**: 调参表 Fix 字段 JSON 字符串可 Parse
  - Given: `"0.95"` → `FixParse`
  - When: 解析调参表的 DEATH_LOSS
  - Then: 值 = `Fix(0.95)`(Q16.16 raw = 62233600)
  - Edge cases: `"1/5"` → `Fix(0.2)`; 浮点字面量 `0.95` → schema 层拒绝(ADR-014)

- **AC-6**: DIAG_TIERS 可查询,35 标记 P1a
  - Given: 注册表已加载
  - When: 读取诊断的档位阈值
  - Then: 返回 {10, 20, 35, 50}; 35 有 P1a 标记
  - Edge cases: 40 级诊断落在 35-50 之间(细档,非检验线)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/skill-system/skill_registry_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None(首批 story)
- Unlocks: Story 002–008(均读取技能注册表)
