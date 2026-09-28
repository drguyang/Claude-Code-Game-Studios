# Story 004: Aggregate、可靠上行与主机落流

> **Epic**: 急救动作模块
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 12h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/emergency-procedures.md`(规则五 结算→病史流 · 规则十一 判定输入的重建契约(R-1 架构级修订)· F-10.5 聚合 · F-10.6 两延迟预算切分 · Edge Cases 重传/多剂)
**Requirement**: TR-emergency-009(EmergencyAttempt 聚合上行)· TR-emergency-010(主机 Judge+Append+Seq)· TR-emergency-011(两支处置事件落流)· TR-emergency-012(七项载荷对齐 11)· TR-emergency-013(Kind 白名单)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-011 **Amendment B**(主;C 路裁定)· ADR-001 **§一之三 裁决一**(判定输入类 → 可靠通道,结清 OQ-10-9 ≡ QQ-14)· ADR-009 **Amendment I**(三新 Kind;一条动作落两条流事件 —— 三段式首次在流层物化两步;有界性 ≤ 每完成动作 2 条,与帧率无关)· ADR-005(主机唯一 Append)/ ADR-006(Seq 发号)/ ADR-024(Kind 经 registry)
**ADR Decision Summary**: 客户端把整个动作期读数**聚合为一条 `EmergencyAttempt` 全整数意图事件**上行(可靠通道)→ **主机执行 `Judge` + `Append` + 发号 `Seq`**;本地判定**降级为预表现**,「不逐帧同步输入」保留。急救侧与 ADR-009 §七 拾取同构(意图 + 当下判距 + 效果进流)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH(联机面 —— 45 网络层 P1b 前只有 pipe 抽象;P0 单机 = 主机自身路径可全测;跨平台重放 AC-10-05 受矩阵前置)
**Engine Notes**: ADR-001 的 `IReplayPipe` Publish/Subscribe + 有界 `ReorderBuffer` 是 45 侧预埋;本 story 对 pipe 只依赖抽象接口,单机路径以 in-process pipe 实现。

**Control Manifest Rules (this layer)**:
- Required: 聚合时刻 = 动作结束(或中止 ⇒ 不发);`mag_peak` 进流、`mag_last` 不进流(仅预表现/48 回放);判定输入**全项可从流重构**(重放 = 同整数 ⇒ 同结果)
- Forbidden: 客户端上报 `JudgeResult`(伪造成本 = 改一个枚举值);逐帧同步输入;绕过 tick 边界的「即时生效」写体征(AC-10-22)
- Guardrail: 重传事件携带原 `dose_seq` ⇒ 主机去重命中拒收(9 的 AC-15 五元组);多剂同 tick = 各得不同 `dose_seq` 叠加

---

## Acceptance Criteria

*From GDD `design/gdd/emergency-procedures.md`, scoped to this story:*

- [ ] **AC-10-05**[A] BLOCKING:同一条病史流(含同一 `EmergencyAttempt` 整数载荷 + 同一烘焙数据集)跨平台重放至动作结束 ⇒ `agg.mag_peak` / `drug_potency` / `half_life` **三整数逐位相同**,`JudgeResult` 相同(三格矩阵实跑子句 NOT-RUN,禁借绿;单机双进程 Mono 重放先行判结构)
- [ ] **AC-10-06**[A] BLOCKING:`EmergencyTreatmentApplied` 载荷七项齐备:`polarity` / `drug_potency` / `half_life` / `处置_id` / `施予者` / `method` / `cause`,且 `Seq` 由主机填充(9 的入向契约 AC-28 联动)
- [ ] **AC-10-06b**[A] BLOCKING:10 与 11 两份载荷定义构建期交叉校验 —— 字段名/类型/序数**完全一致**,除 `method`/`cause`(仅 10 侧)(对照 prescription `AC-11-07` 先例;载荷漂移会静默进 9 的和式)
- [ ] **AC-10-07**[A] BLOCKING:`处置_id ∉ treatable_by(d)` ⇒ **10 照常发事件**(判「有用与否」归 9,非 10)
- [ ] **AC-10-07b**[A] BLOCKING:三 Kind 在 9 的白名单内(DC-5 联动;kindgen 差集断言;R-2 原始症状 = 10 每笔写入被拒)
- [ ] **AC-10-22**[A] BLOCKING:静态检查 —— 不存在绕过 tick 边界的「即时生效」代码路径(10 不得为降 `L_eval` 直写体征;判据 = 10 程序集零 `IVitalsQuery` 写面/零直写符号,反射+IL)
- [ ] **F-10.5 聚合**[L]:`Aggregate` 唯一聚合点 —— `hold_ticks = 末沿−首沿`;`mag_peak = max` 平局取 tick 较小者(禁依赖迭代序);`edge_ticks` 非递减运行时断言(3 侧前提);一条完成动作 ⇒ **恰两条**病史流事件(Attempt + Applied),有界性与帧率无关
- [ ] **可靠上行**[I]:客户端 → 主机走 ADR-001 **可靠通道**(非第二 QoS);断言 = 通道选择静态判据(该 Kind 的路由登记)+ 丢包模拟夹具(in-process pipe 注入丢包 ⇒ 重传后主机去重命中,不双结算)
- [ ] **本地=预表现**[A]:客户端本地 `Judge` 结果**不写流、不发成长**;主机结果到达后以主机为准(可见差异仅一个 tick 的 `L_eval`,F-10.6 切分口径)

---

## Implementation Notes

*Derived from 规则十一 + ADR-011 Amendment B:*

1. `EmergencyAttempt` 载荷 = 聚合后 `agg` 全整数(`action, hold_ticks, edges, edge_ticks[], mag_peak` + 意图侧 `method/cause` 来源标志);**读数原始序列不进流**(聚合即物化,存储有界)。
2. 主机侧管线:`Subscribe(EmergencyAttempt)` → `Judge(agg, action, ctx)` → `Append(EmergencyAttempt 物化)` + `Append(EmergencyTreatmentApplied)` → `Seq` 按 `(Tick, Patient)` 发号(story 002 of disease epic)。
3. `half_life` / `polarity` 来源 = 动作表(story 002 烘焙);`drug_potency` = F-10.4;三者进载荷 ⇒ 9 的 F1/F4 直接消费,零回查 10。
4. 「即时生效」防线落点:10 的呈现只读 `VitalsDto`(边界门面),写路径唯一 = pipe 上行/主机 Append;code review checklist + AC-10-22 断言双保险。
5. P0 联机夹具:in-process 双端(主/客户端)共享 fake pipe;真实网络(45)未落地前集成 AC 的「网络传输」子句记 **BLOCKED-BY-45 epic**,本地双进程子句可跑。

## Out of Scope

- [Story 003]: Judge 本体(本 story 只调用)
- [Story 005]: 跳过/中止的「发/不发」决策(在本 story 的落流入口之前)
- [Story 006]: 预表现呈现与 `L_input` 实测
- 45 网络层 ReorderBuffer / QoS 通道实现(network epic;本 story 依赖抽象)
- 11 的 `DrugTreatmentApplied`(prescription epic;共用 AC-10-06b 校验对)

## QA Test Cases

*Written at story creation(lean mode).*

- **重放逐位**: 固定 `EmergencyAttempt` 载荷 + 固定数据集 ⇒ 双进程重放三整数逐位等(AC-10-05 结构半边);矩阵半边 NOT-RUN。
- **七项齐备**: 每笔 Applied 断七字段非默认 + Seq 单调(story 002 of 9 的发号器联动)。
- **交叉校验红/绿**: 把 11 的载荷影子字段改名 ⇒ 构建期红;复原 ⇒ 绿(AC-10-06b)。
- **越权处置**: `treatable_by` 不含该处置 ⇒ 事件仍发、9 侧离牌门记零贡献(联 9 story 004)。
- **有界性**: 1000 帧动作 ⇒ 流中该动作恰 2 条(与帧率无关)。
- **丢包重传**: fake pipe 丢 Attempt ⇒ 重传携原 dose_seq ⇒ 主机去重拒收,不双结算。
- **无即时路径**: IL 扫 10 程序集零体征写符号(AC-10-22)。

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/EmergencyProcedures/aggregate_stream_test.cs` + `unity/Assets/Tests/PlayMode/EmergencyProcedures/host_authority_test.cs` — must exist and pass;矩阵/网络传输子句 NOT-RUN(显式列出,禁借绿)
**Status**: [ ] Created — NOT STARTED

---

## Dependencies

- Depends on: Story 001(读数)、Story 003(Judge)、disease-simulation epic story 002(流/Seq/去重键)、prescription-medication epic story 003(载荷定义另一方,AC-10-06b 成对);ADR-001 pipe 抽象(45 P0 预埋面)
- Unlocks: Story 005(在其落流口上加发/不发决策)、Story 006(预表现对拍)、case-system epic(处置证据窗口消费 Applied 事件)、telemetry epic(`resolution` 口径)

## Completion Notes
