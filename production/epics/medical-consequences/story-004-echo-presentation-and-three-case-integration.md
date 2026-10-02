# Story 004: 回响呈现世界内与三案链集成走查

> **Epic**: 医疗后果与责任
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/medical-consequences.md`(规则七 可见性 · 规则三 不可归因的呈现面 · 三案链 P0 无世界级后果(F-53.3 P1a)· AC-53-09 [L] / AC-53-10 [L] / AC-53-11 / AC-53-13 [I])
**Requirement**: TR-medcons-007(零可变态零订阅,partial —— 呈现侧消费者 = 13/42/44 的只读纪律联动) · TR-medcons-008(RegionOutcome 载体 —— gap,**P1a,不在本 story**;本 story 只保证 P0 面「无世界级归纳」不误实现)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-013(主): 拟物 UI 框架(42 只渲染不持状态)· ADR-018: 音频架构(无提示音铁律白名单)· ADR-016 §六: 13 消费 `VitalsDto` 的呈现侧 · ADR-009: 表现态不进流(本 story 读流出表现)
**ADR Decision Summary**: 后果的**可见性**(规则七):P0 = 回响**先落在病人身上、并被至少一名在场者先看到**(OQ-53-6 预裁:承载者 = 13 病人 AI / 既有 NPC,实现前已裁方向);**无村落信任值**(AC-53-11:P0 零声誉/信任/累计分的呈现)。呈现三通道:病人姿态与外观(13)、世界内痕迹(42 视觉)、身体声音(44 白名单);**零后果 jingle**(AC-53-10 [L] 听测,联合 AC-44-09)。不可归因在呈现面 = **无可点开的「病例 → 后果」因果链 UI**;玩家只能从世界内线索自行联想。三案链未认出 ⇒ **P0 不产生世界级后果**(归纳是玩家的脑内动作;禁止「不归纳即免疫」的反例走查)。AC-53-13 [I] 集成走查:三案全流程(误诊 → 后遗显形 → 复现)在 playtest 脚本下世界状态一致。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 承载 = 既有 13/42/44 通道(无新引擎面);[L] 走查与 [I] 集成测试为本 story 的判据主体。

**Control Manifest Rules (this layer)**:
- Required: 呈现层只读世界流后果事件(经边界 DTO);回响的可见差异 = 物理形态差异(姿态/斑痕/声音),禁数值浮层
- Forbidden: 任何「后果提示 UI / 因果链视图 / 声望条」;44 侧 sting/jingle/状态播报音;53 持有呈现态
- Guardrail: 「≥1 在场者先见」= 可测前提(OQ-53-6 承载者在场为走查夹具硬条件);呈现不反向写流

---

## Acceptance Criteria

*From GDD `design/gdd/medical-consequences.md`, scoped to this story:*

- [ ] **AC-53-09 [L] BLOCKING 走查**:延迟生效(结案后若干玩家在场的时刻)+ 病人身上可见(13 姿态/外观差分)+ ≥1 在场者先见 + **零归因线索**(无任何 UI/文本指向「因为那例误诊」);录屏 + 主创签核 [L]
- [ ] **AC-53-10 [L] 听测**:回响相关全部音频 ∈ 行为反馈白名单(身体声/环境声);零「惩罚 jingle / 后果 sting」;与 44 的 AC-44-09 断言联跑 [L]
- [ ] **AC-53-11**:代码与资产面零声誉/信任/累计分呈现(扫描词族 {reputation, trust, karma, score…} 于 53/呈现交付物;P0 值 = 不存在而非 0)
- [ ] **AC-53-13 [I] 集成走查**:三案链脚本(同因三例,一误诊)⇒ 世界流含对应 `ConsequenceResolved`、病人态可观察差分、复现窗口按 EmitTick 显形;走查表 + 自动化差分双轨 [I]
- [ ] F-53.3 **不误实现**:三案未认出 ⇒ 无世界级后果(表面对「归纳加速」的预留 = 零);禁止「不归纳即免疫」—— 53 的 per-case 后果事件**照发**(与认出与否无关),此判据从流断言(承 53 件规则八注)
- [ ] 呈现零反渗:13/42/44 读到的后果 DTO 无 case_id / 无数值分数(承 AC-53-07 的呈现侧延伸,复用其扫描器)
- [ ] 支柱面:病人「走了」的呈现 = 世界无慈悲(瘟疫叙事),无「你杀了他」类文本/事件(文案词族扫描 + 走查)

---

## Implementation Notes

*Derived from 规则七/三案链注:*

1. 承载矩阵:后遗 = 病人外观/姿态差分(13 呈现)+ 声(44);复现 = 病程再演(9 侧态,13 呈现);试药史 = 病人对再药的反应差分(呈现走 8/10 既有通道)。53 只保证**事件在流**,差分渲染 = 各呈现系统的既有接口(本 story 接线不新建持有者)。
2. 「在场者先见」夹具:OQ-53-6 预裁承载者 = mentor NPC / 在场病人;走查脚本控制其视线可达(不做 AI 视线判定 —— 那是 13/27 的感知口径,P0 走查用位置事实)。
3. [I] 集成走查 = `tests/integration/medcons/` 脚本 + playtest 记录双轨;数值(EmitTick / 复现窗口)用符号表(未裁 ⇒ 走查用「相对次序」而非绝对时刻,数值轮后回刷)。
4. 因果链 UI 禁项在 42 元件库侧同步登记(联合 AC,证据链在 QA 轮)。
5. 三案链内容(病例文本)归 37;本 story 用其夹具病例,不自撰内容。
6. ⚠️ **数值冻结**:走查中一切「几日后显形」类判断在数值未裁前 = 次序断言;走查单注明「值待数值轮,重跑条件」(免下轮仪式)。

---

## Out of Scope

- [Story 003]: 发出逻辑(本 story 是其下游消费者)
- F-53.3 RegionOutcome 与村落归纳(P1a,TR-medcons-008)
- 31 蓄水池订阅实现(P1a)
- 13/42/44 各自的元件与素材本体(各 epic;本 story 只接回响差分)
- 药物禁忌数值(OQ-53-8 值侧,归数值轮)

---

## QA Test Cases

**[Integration story — walkthrough [L]/[I] + scans]:**

- **AC-1**: 可见性走查(AC-53-09 [L])
  - Given: 三案链夹具,例 2 误诊;在场者 = mentor + 病人
  - When: 结案 → 过 EmitTick(符号表驱动)
  - Then: 病人差分可见(前后截图差分)/ 在场者先见时序成立 / 全程零归因 UI;录屏 + 签核 [L]
  - Edge cases: 无在场者时后果仍发出(流面),仅「先见」延后(走查注记,非不发)

- **AC-2**: 听测(AC-53-10 [L])
  - Given: 同夹具开全音轨
  - Then: 白名单断言(44 联跑)绿;听测单:零惩罚性 jingle [L]
  - Edge cases: 复现显形瞬间 = 身体声变体(非 UI cue)

- **AC-3**: 零声誉呈现扫描(AC-53-11)
  - Given: 53 + 呈现交付物源码/资产
  - When: 词族扫描
  - Then: 零命中(不存在,非恒 0)
  - Edge cases: 调试视图(51)也不得现声誉量(AC-19 面联动)

- **AC-4**: 三案链集成(AC-53-13 [I])
  - Given: 自动化差分:世界流字节 + 病人态快照序列
  - Then: 误诊例 ⇒ `ConsequenceResolved` 在流、差分符合表;未认出三案 ⇒ **无**世界级后果事件但 per-case 后果照发(禁「不归纳即免疫」双向断言)[I]
  - Edge cases: 认出(_patternRecognized_)为加速非开关:发与不发判据与认出无关(承 53 件规则八注)

- **AC-5**: 支柱文案面
  - Given: 全部回响相关文本键
  - Then: 零「玩家致死」归因词;「走了」= 瘟疫叙事框架 [L]

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/medcons/medcons_three_case_chain_test.*` + `production/qa/evidence/medical-consequences/story-004-echo-walkthrough.md`(AC-53-09/10 [L],录屏截图 + 主创签核) — must exist
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003(世界流事件在档)· 13/42/44 呈现通道(并行 epic)· 37 三案链夹具(内容线)
- Unlocks: Epic 收口(P0 面)· P1a 的 31/RegionOutcome 轮(流数据已备)· 数值轮回刷清单(符号表 → 实值)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
