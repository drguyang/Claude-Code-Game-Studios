# Story 001: 体征只读消费与行为映射 —— VitalsDto 取数、双维状态与滞回

> **Epic**: 病人 AI 与行为
> **Status**: Complete ✅ 2026-10-04
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-04
>
> **收口**:双代理单轮评审(结构 **CHANGES REQUIRED** · QA **REJECT**,2 BLOCKING)⇒ 修复轮 **全落实 + 变异坐实**。
> 评审原件 `production/qa/evidence/review-patient-ai-story-001-2026-10-04.md`。
> 实测 **patient-ai 45/45** · 全量 EditMode **2384 / 2340 / 0 / 43 / 1** · 变异 **5/5 各恰一条红**。
> **未闭登记(NOT-RUN,禁借绿)**:AC-13-A1 运行期半边 · 边缘「band 空表」(规格与 readonly struct 形态不匹配)·
> 终态后 `Tier` 规格 · `Material` 正向唯一消费点 · `Emit` 签名类型信息 · QA TC-1/TC-2 —— **均归 story 003**。

## Context

**GDD**: `design/gdd/patient-ai.md`(§Detailed Rules 规则一 13 = 9 的只读消费者 · 规则三-bis `signs[]` 只喂表现不喂决策 · §Formulas F-13.1 `Map(position,trend,prev)`(有记忆,滞回 HYST)· §States 双正交维 `BehaviorState{Idle/Seeking/Bedridden}` + `SessionState{None/InTreatment}` + Terminal 闩锁 · A 组 AC-13-A1/A2/A3/A5 · B 组 AC-13-B1/B5)
**Requirement**: TR-patient-001(13 只读不写 —— 零三流事件、零回写 sim 真值)· TR-patient-002(取数唯一入口 `IVitalsQuery.GetVitals()→VitalsDto`,不读 Fix 原值不绕门面)· TR-patient-003(DTO 只含 position(0–1)/trend 带符号原始量,无病种/九态/派生显示量)· TR-patient-004(13 不重建九态,分档只用自有 `BEHAVIOR_BAND_*`)· TR-patient-005(BAND 与 9 的九态阈值对齐关系 · 现 `gap`,登记不阻塞机制)· TR-patient-008(重建三源不变量,禁第四来源)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-016(主): AI 架构(分层确定性);ADR-027: 13 病人 AI 的写路径归属
**ADR Decision Summary**: ADR-016 §一 裁定「27 的决策进 sim;**13 病人 AI 的决策住边界层(呈现侧)**」—— 13 消费 `VitalsDto`(float,全案唯一浮点出口),**物理上不可能住门 A 的 sim 程序集**;13 仍是派生态,只是不在 sim 程序集内。ADR-027:「查体诱发痉挛」「搬运昏迷病人」两条**写路径皆归系统 10**,13 只出表现(姿态骤变 + 呻吟),8 只声明存在,13 零写。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 住边界/呈现程序集(纯 C# 决策器读 float DTO);`VitalsDto` 的浮点**不进 sim 判定链**(判定只在 DTO 侧);EditMode 可测(注入假 `IVitalsQuery`)。

**Control Manifest Rules (this layer)**:
- Required: 决策输入**恰** = `VitalsDto{position,trend}`(+ 自有滞回 prev 态);`BEHAVIOR_BAND_*` 为自有档位表(烘焙数据);`Map` 带滞回(出带 = 入带 − HYST)
- Forbidden: 13 出现 `IEventSink` / `IEventAuthority` 调用点(AC-13-A1 静态+运行期双守);`signs[]` 进 `Map()` / `BehaviorState` / `IPresentPatients`(AC-13-A5 反射白名单);重建 9 的九态枚举或读 `disease_id`(AC-13-A3)
- Guardrail: 会诊态 `SessionState` **只由边界层显式 `OnExamSessionChanged(id,active)` 赐予**,13 无推断点、幂等、加载后重置 `None`(AC-13-B5)

---

## Acceptance Criteria

*From GDD `design/gdd/patient-ai.md`, scoped to this story:*

- [ ] 静态 + 运行期计数断言:13 对 `IEventSink` / `IEventAuthority` 调用数 = 0;三流零 13 写入(AC-13-A1 / TR-patient-001)
- [ ] 取数唯一入口反射断言:`VitalsDto` 的全部来源恰 = `IVitalsQuery.GetVitals()` 一处;无 `Fix` 原值读取、无门面绕行(AC-13-A2)
- [ ] `Map(position,trend,prev)` 输出唯一确定:EditMode 真值表覆盖边界值与**滞回带内/带外成对输入**,含「position 恰在 band 界、trend 上/下」四象限(AC-13-B1)
- [ ] `signs[]` 消费面白名单:反射断言 `Map()` 输入集**恰 ⊆ {position, trend}**;`signs[]` 仅出现在 F-13.8 材质映射调用点(AC-13-A5)
- [ ] 13 代码类型集无 9 的九态枚举、无 `disease_id`;行为分档常量恰 = `BEHAVIOR_BAND_*` 自有表(AC-13-A3)
- [ ] `OnExamSessionChanged` 三判据:① `SessionState` 写入者唯一 = 该边界层入口(无推断点)② 重复同值幂等 ③ 加载后重置 `None`(AC-13-B5 / EC-13-05)
- [ ] Terminal 闩锁:进入终态后 `Map` 不再改出(单调,与 9 的死亡真值单向对齐)

---

## Implementation Notes

1. 程序集落点:`Gameplay.Presentation`(或专职边界 asmdef,承 ADR-025 清单)——**不在 `Sim`**;依赖注入 `IVitalsQuery` 便于 EditMode 假源。
2. `Map` 的记忆 = 每病人 `prevBand`(滞回用),住 13 自己的派生态字典(不写回 sim);重建期该字典从锚点播种(与 Story 004 的重建口径一致)。
3. band 表(`MILD_MIN` / `SEEK_MIN` / `COLLAPSE_MIN` / `RECOVER_MAX`)与 HYST 为烘焙数据(Fix→DTO 侧以 float 读值比较,档位判定在 13 侧;值归用户数值轮,机制先立)。
4. TR-patient-005(BAND 与九态阈值对齐)现 `gap` 且不阻塞本 story —— 对齐表随数值轮出,机制上 13 不引用九态阈值即可。
5. 反幻想红线:13 永不「治疗」或改变 position —— 它是读数→行为的映射器。

---

## Out of Scope

- [Story 002]: 空间行为(感知/HomeRegion/格步进/LOD)—— 消费本 story 的 BehaviorState
- [Story 003]: 呈现投影视图(ViewState / IPresentPatients / cue / Material)
- [Story 004]: 重建 / 联机权威 / 写路径归 10 的接缝验收
- 9 侧 `VitalsDto` 的产生(归 disease-sim epic)· 10 的急救写路径(归 emergency epic)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 假 IVitalsQuery 产 position 0→1 单调恶化序列 | 连续 Map | 状态只向重档迁移(带滞回台阶),无抖动 |
| TC-2 | position 恰在 SEEK_MIN ± HYST 带内振荡 | Map | 不来回翻档(滞回生效);出档需回落 band−HYST |
| TC-3 | signs[] 含任意词条 | Map / ViewState | 输出与不含时完全相同(signs 不喂决策) |
| TC-4 | 连续两次 OnExamSessionChanged(true) | SessionState | 幂等;加载后 = None |
| TC-5 | 全类型反射扫描 | 断言 | 零 IEventSink 引用点、零九态枚举、零 disease_id |
| TC-6 | 死亡真值(Terminal)后 position 回升假输入 | Map | 终态闩锁不改出 |

**Edge cases**: trend 缺失(首拍)按 0 处理的口径;DTO 越界值(>1 / <0)夹取行为登记;空 band 表装载硬失败。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/PatientAI/behavior_map_test.cs` — must exist and pass
**Status**: [x] Complete — test file exists (`unity/Assets/Tests/EditMode/PatientAI/behavior_map_test.cs`)

---

## Dependencies

**Depends on**: 9 的 `IVitalsQuery` / `VitalsDto` 形状(归 disease-sim,接口已登记)· ADR-013 `PresentationDtoGuard`(递归扫描门已立)
**Unlocks**: Story 002(BehaviorState 驱动空间行为)· Story 003(ViewState 投影)· Story 004(决策轨迹重建的判据对象)

---

## Completion Notes
