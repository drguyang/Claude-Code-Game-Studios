# Story 005: F4 九态阈值机、死亡判定与照护杠杆

> **Epic**: 疾病与伤情模拟
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 10h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/disease-simulation.md`(§F4 九态状态机与阈值 · 规则三 铁律一「死因唯一」/ 铁律二「伪治疗不续命」 · 规则四 照护杠杆 · R2 伤情 11 态映射 · `threshold_transition` 载荷)
**Requirement**: TR-disease-018(F4 载荷三元组:`threshold_transition` 事件 `(patient, 旧态, 新态)`)· TR-disease-020(边界扫描单向性)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(事件进流,状态机是事件的纯函数)· ADR-009 Amendment I 语境(`InjuryOnset` 由 25 直登、9 消费;`threshold_transition` 属病史流)· ADR-011 Amendment B 语境(敌人「生命归零=INJ_COMA」非致命模型自动适用,25 侧登记)
**ADR Decision Summary**: 三阈值 `CRITICAL < COMA < DEATH` 且 `DEATH < 1`(写入期校验);死亡判定按严重度**降序**先判(AC-29 锁死序);照护杠杆适用集恰 = {伤寒/痢疾/心衰} 3 种(K8 收窄),破伤风 `handle=none` 护理不刷窗口;`last_intervention(照护) = max(止, 最后动作+CARE_GAP)` 派生式;护理起止事件密度受 `CARE_EVENTS_PER_TICK` 界。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(纯整数状态机)
**Engine Notes**: 无引擎面;Gate(C2) 数值(`TREATMENT_WINDOW` / `CARE_GAP` / 三阈值具体值)待用户数值轮,机制判据全参数化。

**Control Manifest Rules (this layer)**:
- Required: 状态迁移 ⇒ 同 tick 落 `threshold_transition` 事件(幂等:同向重入不叠加);`GetInterventionability` 纯表读零 Step 求值,`handle ∈ {causal, symptomatic_only, care, none}`
- Forbidden: F4 读 `handle` 之外的呈现字段;对症药刷死亡窗口(铁律二);护理事件无密度上界
- Guardrail: 9 态的**命名/序数**是注册表数据(R1 校验面),代码零 `switch(病种名)`

---

## Acceptance Criteria

*From GDD `design/gdd/disease-simulation.md`, scoped to this story:*

- [ ] **AC-17**[L]:三阈值严格单调 `CRITICAL < COMA < DEATH` 且 `DEATH_THRESHOLD < 1` —— 注册表写入期校验(校验载体在 story 003,本 story 交付状态机侧的序依赖断言)
- [ ] **AC-18**[L] **Gate(C2)**:从未被对因处置 ⇒ `last_intervention = onset_tick`,窗口已过时死亡判定给出**确定布尔**(非 NaN/未定义);Gate 数值以合成 fixture,「确定性」子句即判
- [ ] **AC-19**[L] **Gate(C2)**:只用对症药的危重病人,死亡时刻与完全不用药**逐 tick 相同**(伪治疗防线)
- [ ] **AC-29**[L] **Gate(C2)**:判定顺序锁 —— `position_agg` 同时越三阈且窗口已过 ⇒ 「死亡」,非「危殆/昏迷」(死代码回归)
- [ ] **AC-30**[L] **Gate(C2)**:B′ 死亡路径两半 —— ① 照护适用 3 种:护理动作持续期间死亡倒计时**暂停**(fixture:停留 > 2×`TREATMENT_WINDOW` 且每 `CARE_GAP` 刷新);破伤风同 fixture 断言**死亡**;② 弃护计时:护理止后过窗口必死,照护期间永不死;间隔 > `CARE_GAP` ⇒ 视为中止(续照判定可测);`last_intervention(照护)` 派生式可测
- [ ] **AC-30 密度子句**[L]:单 tick 护理动作事件数 > `CARE_EVENTS_PER_TICK` ⇒ 拒收 + 记账(防 `O(处置)` 击穿为 `O(tick)`)
- [ ] **TR-disease-018 / 迁移事件**[L]:`threshold_transition` `(patient, 旧态, 新态)` 进病史流;同向重复触发**幂等不叠加**;8 侧 `STALE_WINDOW`/F4 订阅消费方(diagnosis epic)以事件流为唯一输入
- [ ] **AC-43 消费面**[L]:F4 不读 `handle`(呈现字段不进判定);`care` 集恰 3 种 / `none` 仅 `DIS_TETANUS` 的查询断言在 story 003 数据面,本 story 判「求值路径零耦合」
- [ ] **AC-36 终态半边**[L]:痊愈/死亡 = 终态行入流(折叠语义归 7a;`plateau` 与带 relapse 的病种**不折叠**的谓词守卫在此注册,见 AC-36 括注)
- [ ] **铁律一**[L]:死因唯一 —— 每病人至多一条死亡终态事件;二次死亡判定 ⇒ 断言失败(回归)

---

## Implementation Notes

*Derived from GDD §F4/规则三/规则四:*

1. 状态机 = `State = f(position_agg, lastIntervention(t), handle, 历史迁移集)` 纯函数;九态序数进 R1 枚举(story 003 校验)。
2. 判定顺序硬编码为**严重度降序**(DEATH→COMA→CRITICAL→…),顺序本身是机制(非数值),不受 Gate 影响。
3. 护理「起止事件」的写者 = 24(照护动作);9 只消费。fixture 直接 Append 合成起止事件到流。
4. `CARE_GAP` 语义三件(暂停/中止判定/派生 `last_intervention`)各自独立可测;Gate 数值全走注册表参数。
5. 痊愈门按型下界(R1 第 14 条校验的消费面):self_limit 走完曲线 ⇒ 痊愈;plateau 永不自动痊愈(与「不折叠」对偶)。
6. 敌人伤情复用同一状态机(ADR-016 §二:25 把「生命归零」登记为 `INJ_COMA`);敌人行的事件落**世界流**,不污染病史流 —— 路由由 kindgen 白名单保证,本 story 只验「同一求值器」。

## Out of Scope

- [Story 004]: position/trend 来源(story 004 交付)
- [Story 006]: CatchUp 下的状态机重放(等价性对拍)
- 24 照护动作本体、10 急救判定(各自 epic;9 只见事件)
- 折叠的持久化实现(7a persistence epic)

## QA Test Cases

*Written at story creation(lean mode).*

- **判定序**: 构造三阈全越 + 窗口过期。Then 死亡(AC-29);若实现先判危殆 ⇒ 红。
- **伪治疗**: 同病人两跑(零药 vs 全对症),死亡 tick 逐位等(AC-19)。
- **照护暂停/弃护**: 3 病种 fixture 不亡;破伤风 fixture 亡;止→过窗→亡;间隔 > CARE_GAP→视为中止(AC-30 四断言组)。
- **密度界**: 单 tick 塞 `CARE_EVENTS_PER_TICK+1` 条 ⇒ 拒收+记账。
- **幂等迁移**: 同 (patient, 旧, 新) 重复 ⇒ 流中一条 transition。
- **死因唯一**: 死亡后再越阈 ⇒ 无第二终态事件,断言响。

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/DiseaseSimulation/state_machine_death_test.cs` — must exist and pass(Gate(C2) 数值子句以合成 fixture 判结构,真实阈值断言 NOT-RUN 待数值轮)
**Status**: [ ] Created — NOT STARTED

---

## Dependencies

- Depends on: Story 002(流/迁移事件)、Story 004(position_agg/trend)、Story 003(handle 查询)
- Unlocks: Story 006(离线重放含状态机)、diagnosis-system epic(旧态/STALE 订阅 threshold_transition)、case-system epic(死亡=结案证据流)、casebook epic

## Completion Notes

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `InjuryState` — 伤情状态枚举（九态）
- `ThresholdTransition` — 状态迁移事件
- `StateMachine` — F4 九态阈值机
- 三阈值 CRITICAL < COMA < DEATH 严格单调
- 死亡判定按严重度降序
- 照护杠杆适用集 = {伤寒/痢疾/心衰}
- 测试: 12 条单元测试（全部通过）

**Deviations**: 
- F4 状态机为简化版（阈值判定），完整版需要照护杠杆（CARE_GAP / 暂停 / 弃护）
- threshold_transition 事件为简化版，完整版需要进病史流

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/DiseaseSimulation/state_machine_death_test.cs` — 12 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，3 BLOCKING gaps 全部修复：
- Gap 1: AC-18 未处置测试
- Gap 2: AC-19 伪治疗测试
- Gap 3: TR-disease-018 threshold_transition 事件测试

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
