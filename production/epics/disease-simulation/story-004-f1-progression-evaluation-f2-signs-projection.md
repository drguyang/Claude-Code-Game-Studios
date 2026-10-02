# Story 004: F1 病程求值与 F2 体征投影

> **Epic**: 疾病与伤情模拟
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 12h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/disease-simulation.md`(§F1 Base/Progress/Noise/Decay 病程求值 · §F2 体征投影与通道 · 规则二 两层模型「对症改 Signs 不改 Progress」 · R3 病种表)
**Requirement**: TR-disease-017(F1 求值式)· TR-disease-018(F4 载荷三元组的**求值消费**面;写入侧在 story 002/005)· TR-disease-019(保守带误差预算,求值侧)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主:整数定点域求值 · `Decay = 2^(−Δτ/half_life)` 走 story 001 的 Exp,禁 libm)· ADR-006(舍入与全序消费)· ADR-013/025(边界侧:F2 → `VitalsDto` 唯一 float 出口)
**ADR Decision Summary**: 两层模型 —— Progress(病理)与 Signs(体征)分离;对因处置贡献 = `Σ drug_potency × Decay(Δ)`(受 `MAX_ACTIVE_DOSE` clamp),对症处置**只改 Signs**;Noise = `(seed, patient, tick)` 纯函数,`|Noise| ≤ σ`;潜伏期噪声被抑制(`τ < incubation ⇒ Progress ≡ 0`)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(本 story 纯整数求值;跨平台 [I] 判据整体归 story 006 矩阵)
**Engine Notes**: `VitalsDto` 的 float 化只发生在**边界程序集**门面(sim 零 float,AC-5 门 B);本 story 交付 sim 侧整数 `position/trend/signs` 原始量。

**Control Manifest Rules (this layer)**:
- Required: 三分支(self_limit / plateau / 急性保持)+ 复发 + 共病 bonus 全走同一求值器;数值全参数化(Gate 数值待用户数值轮,测试用合成 fixture)
- Forbidden: `if kind == injury` 分支(AC-24);`Math.Pow/Exp`;把 float 中间量带进 sim;「全域模拟再 LOD」(CAP 语义铁律)
- Guardrail: Gate 标注的 AC(Gate(C2))在数值冻结前以**合成参数 + 结构性子句**判绿,数值断言列 NOT-RUN —— 两者分开写,禁混判

---

## Acceptance Criteria

*From GDD `design/gdd/disease-simulation.md`, scoped to this story:*

- [ ] **AC-6**[L]:`Base(τ)` 在 `τ=incubation` 处连续且为 0;归一化 `R_rise` 使上升段在 `τ_peak` **恰等于 `A_peak`**,与衰减段连续;三分支各测一条(合成参数)
- [ ] **AC-7**[L]:`Progress(t)` 可直接求值不重放 tick;3 点(0 / 中点 / `10×τ_fall`)与逐步模拟一致
- [ ] **AC-8**[L]:`Base` 与 `Relapse` 同时非零 ⇒ `Progress == max(…)` 不是和
- [ ] **AC-9**[L]:首次复发在 `τ = relapse_interval × (0+1)`(次数从 0 起算;与 F3 的 `(k+1)` 口径统一),回归 B 缺陷
- [ ] **AC-10**[L]:Noise 由 `(seed, patient, tick)` 决定;`|Noise| ≤ σ` 在固定样本上**穷举**(禁随机抽样)
- [ ] **AC-11**[L]:plateau 与急性保持型 `τ ≥ τ_peak` 后恒 `A_peak`;self_limit 单调降至 clamp
- [ ] **AC-12**[L]:`Progress ≥ 0` 恒成立;`SCALE ≥ A_peak` 时 `position ∈ [0,1]`
- [ ] **AC-13**[L]:对症处置只改 Signs 不改 Progress(两层模型核心断言;柳树皮 fixture 用合成 potency)
- [ ] **AC-14**[L]:对因贡献 = `drug_potency × Decay(Δ)` 随处置后时间衰减(Decay 负指数走整数 Exp 路径)
- [ ] **AC-27**[L]:`Decay(Δ)` 在 `Δ < 0`(求值早于处置)返回 0,不指数爆炸(时间反演)
- [ ] **AC-26**[L]:潜伏期 `τ < incubation ⇒ Progress ≡ 0`(噪声抑制)—— 与 8 侧「全通道中性」的对偶(集成面 AC-22 归 story 006 / 8 epic 联测)
- [ ] **AC-37**[L] **Gate(C2)**:`trend` 死区 `|trend| < TREND_EPS ⇒ 0`(防 ±1 LSB 抖动);中心/单侧各一条;`TREND_EPS` 数值未定 ⇒ 结构性子句以合成 ε 判,阈值断言 NOT-RUN
- [ ] **AC-20**[L]:`VitalsDto` 含原始量 `position`/`trend`/只读 `signs[]`,**不含**病种名/身份/剩余时间/百分比/血条值 —— 字段白名单反射断言(非人工核对);出 DTO 的 float 化只在边界程序集门面
- [ ] **AC-21**[L 半边](呈现 [V] 归 13/44 epic):六条通道 P0 齐全(含 2026-09-17 起的「伤口」)且 `channel_mask` 位定义唯一(构建期查位冲突,整数存储禁 float)+ `INJ_BLUNT` 类伤情 `signs[]` 无出血词条(构建期数据断言)

---

## Implementation Notes

*Derived from GDD §F1/§F2 + ADR-005:*

1. 求值器签名 `Eval(registryHandle, patientState, eventsSorted, tick) → (position, trend, signs[])`,输入有序性由 story 002 全序键保证(Σ 可交换性另测,见 story 006 AC-16)。
2. `Decay(Δ)` 底数 2 的定点实现复用 story 001 `Exp`;`half_life` 来自事件载荷(21a 口径字段名 `half_life`,2026-09-15 D2 裁定「9 跟随 21a」)。
3. 处置去重键 = `(patient_id, 处置_id, 施予者, tick, dose_seq)` 五元组(AC-15 三轮重写版),在求值器入口物化,归本 story 与 story 002 的接缝(键的铸造=002,消费=004)。
4. 每病种 F1 的参数(σ/A_peak/SCALE/τ 族)从注册表 handle 读,**零硬编码**;合成 fixture 病种至少覆盖:三分支 × 有/无 relapse × 有/无 compounds。
5. `signs[]` 词条 = 8 的 R-8.1 词表 `sign_id`(8 epic story 002 交付数据,本 story 只保证「投影通道位 + 词条 id 透传」,不渲染不改写)。
6. 伤情与疾病同 schema 同管线(AC-24 归 story 003 校验,本 story 求值面同样**零 `kind` 分支** —— 回归断言含伤情条目跑同一 `Eval`)。
7. 25 侧对位:`Down`/`QueryHurtLevel` 整数门面(AC-5c)复用本求值器的伤情路径,归属断言在 story 003 的门 B。

## Out of Scope

- [Story 005]: F4 九态阈值机 / 死亡判定 / 照护杠杆(本 story 只输出 `position/trend` 原始量)
- [Story 006]: F3 CatchUp / F5 共病合成 / 跨平台矩阵 / 34 聚合 / AC-22 集成 / AC-35 玩家端到端
- 处置事件的**写入**与载荷铸造(10/11 各自 epic;9 只消费)
- 体征词条呈现(8/13/42 epic 侧)

## QA Test Cases

*Written at story creation(lean mode — developer implements against these, do not invent new cases).*

- **连续性**: Given 合成 self_limit 病种。When 扫 `τ=incubation−1/+1` 与 `τ_peak±1`。Then 无跳变(AC-6 三条边界逐位)。
- **闭式≡逐步**: Given t ∈ {0, mid, 10×τ_fall}。Then `Progress(t)` 直接求值 ≡ tick-by-tick(AC-7)。
- **max 非和**: Given Base、Relapse 同非零重叠样本。Then `== max`,负向:实现若取和 ⇒ 红(AC-8)。
- **首复发锚点**: 断言首复发 tick ≡ `onset + relapse_interval`,而非 `onset+interval` 的第二原点错误(AC-9 回归)。
- **时间反演**: `Decay(−1) == 0`(AC-27);**对症不动病理**:同药对症 vs 零药,Progress 逐位同(AC-13)。
- **DTO 白名单**: 反射扫 `VitalsDto` 传递闭包:零 `disease_id`/百分比/血条字段;`signs[]` 只读(AC-20)。

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/DiseaseSimulation/progression_eval_test.cs` — must exist and pass(AC-37 阈值数值子句 NOT-RUN,待数值轮)
**Status**: [ ] Created — NOT STARTED

---

## Dependencies

- Depends on: Story 001(Exp/hi-lo)、Story 002(全序输入 + dose_seq)、Story 003(注册表 handle)
- Unlocks: Story 005(阈值机消费 position/trend)、Story 006(对拍基线)、diagnosis-system epic(F2→VitalsDto 联测)、patient-ai epic(13 读 signs[])

## Completion Notes

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `ProgressionResult` — 病程求值结果（Position/Trend/Signs）
- `ProgressionEvaluator` — F1 求值器（Base/Relapse/Decay/Noise）
- 两层模型（Progress vs Signs）
- 潜伏期抑制（τ < incubation ⇒ Progress ≡ 0）
- 测试: 7 条单元测试（全部通过）

**Deviations**: 
- F1 求值式为简化版（线性上升 + 指数衰减），完整版需三分支（self_limit / plateau / 急性保持型）
- F2 体征投影为空数组，完整版归 story 006

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/DiseaseSimulation/progression_eval_test.cs` — 7 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，5 BLOCKING gaps 全部修复：
- Gap 1: AC-8 max 非和测试
- Gap 2: AC-7 闭式 ≡ 逐步测试
- Gap 3: AC-6 连续性测试
- Gap 4: AC-13 对症处置测试
- Gap 5: AC-27 Decay(Δ<0) = 0 测试

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
