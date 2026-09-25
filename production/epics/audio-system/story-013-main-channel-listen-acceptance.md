# Story 013: 主通道听测验收

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(系统分类;验收面落表现层)
> **Type**: Visual/Feel
> **Estimate**: 4h(含被试排期)
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/gdd/audio-system.md`(§Player Fantasy「门后的人还在喘气」· AC-44-17)
**Requirement**: TR-audio-011(世界语境呼吸 = 幻想主通道的**可验形态**)

**ADR Governing Implementation**: ADR-018: 音频架构(主:主通道有总线 AC-44-D8;本 story 补**听得见**的验收)
**ADR Decision Summary**: 幻想的最终判据 = 玩家隔门听出转危 —— 首评阻断 4 修出 F-44.7 公式,二轮 F6 补接线(008/16)与本听测(17),验收链闭合。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(纯验收,无新引擎面)
**Engine Notes**: 听测为 [L] 人工证据(门级 = 人工签署,非 BLOCKING 断言 —— 硬标 [L] BLOCKING = 谎报可测试性);协议承 qa 二轮具体化(双盲 forced-choice)。

**Control Manifest Rules (this layer)**:
- Required: 证据落 `production/qa/evidence/` + 签署 · 协议 = 双盲 forced-choice(n ≥ 6 × 三对 ≥ 8 次)
- Forbidden: 以「接线断言绿」代替听测(AC-44-16 与 AC-44-17 合起来才是验收)· 听测无对照组
- Guardrail: 分叉的只有音 —— 听测在单机本机档做(D-A 语义,不引入联机变量)

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [ ] **AC-44-17** [L]:GIVEN 病人呼吸三态(平稳 → 急促 → 停顿),WHEN **双盲 forced-choice** 听测(隔一道门/墙;三对判别 (0,1)(1,2)(0,2);n ≥ 6 人 × 每对 ≥ 8 次),THEN **判对率 ≥ 75% 且三态均可辨** —— `production/qa/evidence/main-channel-listen-evidence.md` + 签署。

---

## Implementation Notes

*Derived from GDD §Player Fantasy + AC-44-17(2026-09-25 二轮 F6 新增)*:

- 本 story 几乎零代码 —— 它验收 Story 004(呼吸语义素材)+ Story 008(空间化接线)+ Story 009(交接后稳定态)的**合成效果**;若有测试脚手架(自动生成三态音频夹具段)属本 story。
- 协议落地:素材段 = 同一病人的三态呼吸(9 的语义 → 13 表现映射产出);门 = 真实房间几何或等效遮挡(occlusion 路径全开);判对率计算入 evidence 文档。
- 「停顿(昏迷)vs 停止(死亡)」的成对判别可与 OQ-44-7 spike(尾音设计)合并执行 —— 该 OQ 的 fallback(「看动画」)在隔墙场景不可用,听测结果直接决定 OQ-44-7 走向。
- 失败分支:判对率 <75% ⇒ 调 `tier_map`/素材(OQ-44-1 同族)或重评 F-44.7 衰减/遮挡参数 —— 结果回灌 §Tuning。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: 贴耳听诊层听测(AC-02/08)—— 本 story 是**隔门**世界路径
- Story 008: 接线与空间化实现(16 的 [A] 半)
- 内容批:三态呼吸素材录制若缺(由 004/010 素材面供)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-17**: 双盲听测。
  - Setup: 病人三态音频段(同素材源,仅态别不同);隔门场景;被试 n ≥ 6,不参与素材制作。
  - Verify: 三对 forced-choice(0,1)(1,2)(0,2),每对 ≥ 8 次/人;记录对/错与置信。
  - Pass condition: 总判对率 ≥ 75% 且每对单独判对率 ≥ 65%(三态均可辨,防一对拉平);证据 + 签署入 `production/qa/evidence/main-channel-listen-evidence.md`。
  - Edge cases: 被试可见提示/视觉线索 ⇒ 该轮作废(盲法破坏);参数调过(TierMap/衰减)后须**重跑**(旧证据不可复用)。

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**:
- `production/qa/evidence/main-channel-listen-evidence.md` — 听测数据 + 签署(ADVISORY 门级)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002 · 004(呼吸语义与素材面)· 008(接线)· 009(交接后稳定)· 010(素材就位)
- Unlocks: EPIC 44 DoD(36 AC 全验的最后一块)
