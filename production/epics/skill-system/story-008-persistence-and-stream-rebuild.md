# Story 008: 技能状态持久化与流重构

> **Epic**: 技能与熟练度
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-27

## Context

**GDD**: `design/gdd/skill-system.md`(§3.2 成长规则 · §8 功能验收)
**Requirement**: TR-skill-008(技能成长的存档持久化:SkillGrown 落病史流、不独立快照、从流重构)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-010(主): 7a 持久化与存档格式 · ADR-009: 世界状态的事件化边界(三流不折叠)· ADR-024: 三流 Kind 单一登记真源
**ADR Decision Summary**: 技能等级从病史流的 SkillGrown 事件重构(不独立快照);SkillGrown 不随病人折叠被抹除(折叠豁免扩一类 Kind,结清 OQ-7a-9);义务 14 入 ADR-010 §三;三流序列化由 7a 全二进制 codec 完成。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 持久化面归 7a(独立系统);30 只提供「从 SkillGrown 流重建技能等级」的纯函数。零 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: 技能等级从 SkillGrown 流重构(不独立快照);SkillGrown 折叠豁免(不随病史流病人折叠)
- Forbidden: 技能等级独立快照(与「从流重构」直接冲突);SkillGrown 被折叠谓词抹除
- Guardrail: 重建结果 = 从头 replay 全部 SkillGrown 事件(逐位一致,与初始状态无关)

---

## Acceptance Criteria

*From GDD `design/gdd/skill-system.md`, scoped to this story:*

- [ ] 技能等级可从病史流的 SkillGrown 事件**从头重建**(无初始快照)
- [ ] 重建函数输入 = 完整 SkillGrown 事件序列(按三流全序排列),输出 = {skillId: level} 字典
- [ ] 同一 skill_id 的多条 SkillGrown → 取最新 Level(按 Tick 序)
- [ ] 掉级事件(Level 低于前值)正确反映 → 等级下降
- [ ] 存档后读档:技能等级与存档前一致(round-trip 通过 7a 全二进制 codec)
- [ ] 折叠后重建:即使病史流折叠了旧病例行,SkillGrown 行保留,重建结果正确
- [ ] 新玩家(无 SkillGrown 历史) → 全部技能等级 = 0(或初始值,看调参表定义)

---

## Implementation Notes

*Derived from ADR-010 / ADR-009 Implementation Guidelines:*

1. **`RebuildLevels(IEnumerable<SkillGrownPayload> events)`** → `Dictionary<int, int>` —— 纯函数,输入 SkillGrown 事件序列,输出各技能最新等级。
2. 算法:遍历事件序列(按 Tick 升序),对每条 SkillGrown 取其 `Level` —— 如果 Level ≠ 哨兵(-1),则更新 `skillLevels[skill_id] = Level`。最终字典即为当前等级。
3. **为什么「取最新 Level」而不是「累加 XP」**:SkillGrown 携带的是绝对等级(升级时携带新等级,未升级写哨兵)。重建时只看最新 Level 即可 —— 这正是「不独立快照」的含义。
4. **与 7a 的接缝**:7a 负责从二进制存档中读出病史流(含 SkillGrown);30 提供 `RebuildLevels` 纯函数。7a 把重建后的等级字典注入 30 的状态。
5. **折叠豁免**:ADR-010 的折叠谓词**豁免 SkillGrown Kind** —— 即使病例行被折叠,SkillGrown 行保留。这是 ADR-026 §七 的裁决(结清 OQ-7a-9)。
6. **初始状态**:新玩家 / 新存档 → 空 SkillGrown 序列 → 全部等级 = 0(或调参表定义的起始值)。无「默认快照」。

---

## Out of Scope

- [Story 003]: XP_to_next 曲线(重建后计算下一级阈值,但曲线本身独立)
- [Story 005]: 死亡掉级逻辑(掉级 emit SkillGrown,重建消费它,但逻辑各自独立)
- [Story 007]: SkillGrown 事件发射(本 story 是消费者,非生产者)

---

## QA Test Cases

**[Integration story — automated test specs]:**

- **AC-1**: 从头重建技能等级
  - Given: SkillGrown 序列: [(诊断, Level=1), (诊断, Level=2), (急救, Level=1), (急救, Level=1-哨兵), (急救, Level=2)]
  - When: RebuildLevels(events)
  - Then: {诊断: 2, 急救: 2, ...其他: 0 或未出现}
  - Edge cases: 空序列 → 全部 0

- **AC-2**: 未升级事件(哨兵)不影响等级
  - Given: SkillGrown: [(诊断, Level=5), (诊断, Level=-1-哨兵), (诊断, Level=6)]
  - When: RebuildLevels(events)
  - Then: {诊断: 6}(哨兵被忽略,最新有效 Level=6)
  - Edge cases: 全部为哨兵 → 等级 = 0(未升级过)

- **AC-3**: 掉级事件正确反映
  - Given: SkillGrown: [(徒手, Level=10), (徒手, Level=7)(死亡掉级)]
  - When: RebuildLevels(events)
  - Then: {徒手: 7}(取最新 Level,不管方向)
  - Edge cases: 连续掉级:10→8→5 → 最终 5

- **AC-4**: 存档 round-trip 一致
  - Given: 技能等级 {诊断: 25, 急救: 18, ...},经 7a 写入二进制存档再读出
  - When: 重建读出的 SkillGrown 序列
  - Then: RebuildLevels → {诊断: 25, 急救: 18, ...}(与存档前一致)
  - Edge cases: 7a 的 Fix 编码器往返 raw long 逐位一致(ADR-006)

- **AC-5**: 折叠后重建正确
  - Given: 病史流折叠了病例行,但 SkillGrown 行保留;SkillGrown 序列完整
  - When: RebuildLevels(折叠后的 SkillGrown 序列)
  - Then: 结果与未折叠一致(折叠豁免生效)
  - Edge cases: 极端情况:全部 SkillGrown 行保留但病史流只剩 1 条 → 重建仍正确

- **AC-6**: 新玩家初始状态
  - Given: 空 SkillGrown 序列(新玩家,无历史)
  - When: RebuildLevels(empty)
  - Then: 全部 19 项技能等级 = 0(或调参表起始值)
  - Edge cases: 新存档 + 死亡一次 → 仍为 0(0 × 0.95 = 0)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/skill-system/persistence_rebuild_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 007(SkillGrown 事件可写) · Story 005(掉级触发 SkillGrown)
- Unlocks: None(末条 story,技能系统核心闭环)
