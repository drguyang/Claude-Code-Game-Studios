# Story 002: 单次经验增益公式

> **Epic**: 技能与熟练度
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/skill-system.md`(§3.2 成长规则 · §4.1 单次经验 · §4.2 升级曲线)
**Requirement**: TR-skill-002(熟练度成长在整数定点域内求值) · TR-skill-005(运行时调参表的默认值须定点化)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-026(主): 技能成长的定点化与持久化契约
**ADR Decision Summary**: XP_gain = BASE × K_difficulty × K_novelty 全部在 Q16.16 整数定点域求值;乘法用 Fix.Mul(中间积 Q32.32 再移位回);BASE 为 Fix 常量(JSON 写字符串→FixParse);K_difficulty 由调用方(如 8 诊断)推入,K_novelty 三档系数由 30 内部查表。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯定点乘法,不触及任何 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: XP_gain 全路径 Fix.Mul(禁止混合 int×Fix 未转换);BASE 为 Fix 常量,K_difficulty 为 Fix 入参
- Forbidden: 任何 float/double 出现在 XP_gain 计算链;Math.Pow/Math.Exp 用于成长计算
- Guardrail: K_difficulty 范围 1.0~3.0(调用方负责入参合法性,30 只做乘法)

---

## Acceptance Criteria

*From GDD `design/gdd/skill-system.md`, scoped to this story:*

- [ ] `XP_gain = BASE × K_difficulty × K_novelty` 在 Q16.16 定点域求值,结果 = Fix 类型
- [ ] BASE 默认值(§4.1 表格 19 项)全部可配置,读取自调参表
- [ ] K_novelty 三档系数正确: 首次 = 3.0 / 冷却内 = 0.2 / 其它 = 1.0
- [ ] K_difficulty 作为外部入参(由 8 诊断或其他调用方推入),30 不自行计算
- [ ] 诊断错误/辨证错误时 K_difficulty = 0.5(调用方负责传入,30 只做乘法)
- [ ] 技能到 SKILL_CAP(60)后,经验被丢弃(不影响累计 XP,仅不发放)
- [ ] 同一技能同一对象在冷却期内 → novelty_class = stale → 系数 0.2
- [ ] 定点求值结果与手工高精度计算逐位一致(测试用例覆盖边界值)

---

## Implementation Notes

*Derived from ADR-026 Implementation Guidelines:*

1. **`ComputeXpGain(skillId, objectId, noveltyClass, difficulty)`** 是唯一公开入口 —— 返回 `Fix`(该次事件应得的经验值)。
2. `BASE[skillId]` 读自调参表(`Fix`);`K_difficulty` 为 `Fix` 入参;`K_novelty` 由 `NoveltyClass` 查表得到(`Fix` 常量)。
3. 三次 Fix.Mul: `BASE × K_difficulty` → 中间积 Q32.32 → 移位回 Q16.16 → `× K_novelty` → 最终值。
4. **零分配**:返回栈分配 `Fix` 值类型,不分配托管内存。
5. `SKILL_CAP` 上限检查发生在**经验发放前**( caller 判断 `currentLevel < SKILL_CAP` 才调用本方法),本方法**不做上限截断** —— 单次 XP_gain 可以超出到下一级的阈值,那是 Story 003 的职责。
6. `noveltyClass` 三值枚举: `First = 0`(×3.0)、`Stale = 1`(×0.2)、`Normal = 2`(×1.0)。
7. **与 Story 006 的接缝**:`noveltyClass` 由 Story 006 的 `NoveltyTracker` 判定后传入;本 story **不持有**新颖度字典。

---

## Out of Scope

- [Story 001]: 技能注册表与调参表加载(本 story 的输入依赖)
- [Story 003]: XP_to_next 曲线与升级判定
- [Story 006]: 新颖度字典追踪与冷却期管理(本 story 只消费 noveltyClass 枚举)

---

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 18/18 passing
**Deviations**: None
**Test Evidence**: Logic: `unity/Assets/Tests/EditMode/SkillSystem/xp_gain_test.cs` — 18 tests, all passing
**Code Review**: Unity-specialist APPROVED + qa-tester reviewed (3 GAP tests added)

## Dependencies

- Depends on: Story 001(技能注册表与调参表)
- Unlocks: Story 003(升级曲线消费 XP_gain)
