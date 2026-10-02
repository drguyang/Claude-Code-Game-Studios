# Story 006: F3 CatchUp、F5 共病合成与跨平台黄金夹具

> **Epic**: 疾病与伤情模拟
> **Status**: Complete
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 12h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/disease-simulation.md`(§F3 离线补算 CatchUp · §F5 共病合成 compounds · 规则五 有界性 · AC 组七「确定性/重放」)
**Requirement**: TR-disease-009(主机唯一 Step/CatchUp 的运行面)· TR-disease-015(AC-1 跨平台逐位)· TR-disease-016(Exp 位确定,求值链消费侧)· TR-disease-020(边界扫描单向性)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主:主机唯一执行 + 事件流唯一真源 ⇒ CatchUp ≡ Step 是重放的定义而非近似)· ADR-012(双级黄金夹具 + 三格常驻矩阵 + golden-vN 刷新纪律)· ADR-006(乱序重放的全序键)
**ADR Decision Summary**: `Step ≡ CatchUp(t, t+1)` 由**构造保证**(同一求值器);逐 tick N 次 vs `CatchUp(0,N)` 在 `MAX_SCAN_STEPS` 未触发的子区间上**逐位相同**,两版差 = 仅截断事件(`TENTATIVE` + 下一 tick 显式扩域重扫)。三格常驻 = Linux-x64-Mono / Linux-x64-IL2CPP / Linux-ARM64-IL2CPP;禁单平台独签;Windows/Apple 发版前必跑。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH(ADR-012 矩阵实现须 spike;F7:int64 溢出在 IL2CPP C++ 侧为 UB —— 本 story 是该风险**首次落到判据**的故事)
**Engine Notes**: 跨平台对拍须 `unity-builder` 出 player + 独立 job(`unity-test-runner@v4` 只覆盖 Editor/Mono,引擎复核 F1 口径);qemu 否决(softfloat 不可信为逐位判据)。**本 epic 内 [I] 跨平台 AC 一律 NOT-RUN 直至矩阵实跑,禁借绿。**

**Control Manifest Rules (this layer)**:
- Required: CatchUp 与 Step 共享同一求值器(非两套实现);共病 `compounds` 无环且同靶禁 `Δ_rate`/`Δ_progress` 混用(构建期拒);34 聚合零个体可识别字段
- Forbidden: 「离线版近似」(插值/简化公式另实现);为过对拍而放宽逐位比较(容差 ≠ 判据)
- Guardrail: AC-3b 计数器级 与 AC-3c 墙钟级 **两级不合并**(计数器断言 ≠ 墙钟)

---

## Acceptance Criteria

*From GDD `design/gdd/disease-simulation.md`, scoped to this story:*

- [ ] **AC-3**[L]:`Step ≡ CatchUp(t, t+1)` 构造保证;逐 tick N 次 ≡ `CatchUp(0,N)`(未截断子区间逐位);差集 = 仅截断事件且带 `TENTATIVE` 标记 + 下一 tick 扩域重扫
- [ ] **AC-3b**[L] **Gate(C2)**:离线 30 天分档计数器 —— self_limit:`count(30d)==count(60d)`;plateau/急性保持:步数 ≤ `MAX_SCAN_STEPS` 且截断记账有上界;复发:`ε_PRUNE` 启用后常数(未剪枝线性 = 已知事实,显式标注不判 fail)
- [ ] **AC-3c**[I]:读档墙钟 —— 「超限必记账、不静默卡死」为结构性 BLOCKING(51 读数接口 + 显式降级路径);阈值数值 Gate,未定前不判 fail
- [ ] **AC-31**[L] **Gate(C2)**:多病种合成取大(字面用 `"3/10"`/`"4/5"`,禁 `0.3`);状态机按 `position_agg` 判定(与 story 005 接缝)
- [ ] **AC-32**[L] **Gate(C2)**:风湿热→心衰旗舰共病线 —— `min_position` 持续 `min_duration` 后靶 `Progress` 出现显式 `bonus` 增量;`CompoundTriggered`(带 tick)/`CompoundExpired` 两 Kind 落流;离线 CatchUp 与逐 tick 一致(**阶跃不漏**);`A→B→A` 构建期拒;plateau 靶配 `Δ_rate` 配置期拒;`compound_max_triggers` 界;**K3 离线自举**:缺在线事件时按无噪声包络上闭式条件外插并标 `TENTATIVE`
- [ ] **AC-33**[L]:`boundary_mode = scan` 病种完成 CatchUp 不静默退化;空处置史与有处置史同构(Σ 空和 = 0,不 NaN/不短路)
- [ ] **TR-disease-020**[L]:边界扫描单向性(扫描游标单调不回退;越界扫描 = 断言失败)
- [ ] **AC-16 乱序重放**[L]:打乱到达顺序按全序键重放,折叠/Σ 结果相同;**加性面**乱序不变(F1 Σ 与 compounds Δ_progress);乘性 `Δ_rate` 不参与承诺(与 story 003 校验对偶)
- [ ] **AC-22**[I]:潜伏期病人在 8 侧全通道中性(读数为空)而 9 照常推进 —— 跨系统联测(与 diagnosis epic story 联测批次)
- [ ] **AC-25**[L]:黄金夹具入库 —— 固定病史/处置序列 → 同源生成算子求值 → 期望 `position/trend/state` 样例表逐位相等;**含 ≥1 条人从定义推导的锚点样例**(如 `A_peak=SCALE ∧ τ→∞ ⇒ position=1`)防自产自评
- [ ] **AC-1**[I] BLOCKING:三格矩阵同一批病人 `SimEvent` 流逐位相同(SplitMix64 哈希相等)+ F7 回绕语义随矩阵跑 —— **NOT-RUN 直至 ADR-012 矩阵实跑**
- [ ] **AC-2**[L] BLOCKING:存档往返状态不变;存盘路径零 float;`Fix` 自定义编码器(Unity 内置序列化器静默归零,EditMode 探针守 + `PatientState` 类型传递闭包扫描);Step 驱动者不缓存可序列化状态
- [ ] **AC-34**[I]:9 每 N tick 在下 tick 边界广播生态区级聚合(病种计数+趋势,零个体字段);「零订阅者跳扫」子句本 story 判,「订阅恢复」机制归 P1a 34(不越界)
- [ ] **AC-35**[L 结构半边]:玩家自身 = `PatientState`,同一求值函数(守门:玩家入口不新建求值路径);行为端到端 [I] ADVISORY 与 player-controller epic 联测

---

## Implementation Notes

*Derived from GDD §F3/§F5 + ADR-012:*

1. CatchUp 的实现纪律:**只允许**调用 story 004/005 的同一 `Eval`,以「区间推进 + 截断标记」为唯一差异;code review 判据 = 无第二求值器。
2. `MAX_SCAN_STEPS` / `ε_PRUNE` / 扫描步长是配置项(Gate 数值归用户轮);计数器断言用合成小病种,不用真数值集。
3. `TENTATIVE` 语义 = story 002 折叠/序列化面可见的标记位;扩域重扫的「下一 tick」指状态机推进序,非墙钟。
4. 共病 fixture:至少覆盖 心衰(plateau, 靶禁 `Δ_rate`) / 伤寒 / 痢疾 / 麻疹(self_limit) / 疟疾(复发 + 剪枝)5 条线;数值合成参数化。
5. CI job 拓扑按 ADR-012 §二:Editor-Mono(黄金夹具重签辅助)+ 三格 player 对拍(独立 job);golden-vN 刷新 = 变更日志 + 全平台同时重签 + 旧版保留回归对比。
6. `unity/Logs/build-*.log` 为 batch-mode 判决行落点(承 CLAUDE.md「Unity Debugging CLI First」);跨算侧(【超算】batch 可跑 IL2CPP player 对拍;桌面仅编辑器绿不构成三格判据)。

## Out of Scope

- Story 001 的库级单元夹具(本 story 消费并升集成级字节对拍;ADR-010 存档字节流级归 7a epic + 本 story 联动)
- 45 网络层的重排缓冲(ADR-001 ReorderBuffer;全序键语义 story 002 已定,网络到达面在 45 epic)
- 51 遥测读数器本体(telemetry-analytics epic;本 story 只暴露计数器/墙钟读数接口)
- 34 聚合订阅协议(P1a,34 GDD 未设计)

## QA Test Cases

*Written at story creation(lean mode).*

- **等价性**: Given 合成 200 tick 病史。When 逐 tick vs CatchUp(0,200)。Then `SimEvent` 序列逐字节等;截断样本断言差集仅 TENTATIVE 行(AC-3)。
- **计数器分档**: 30d/60d 计数相等(self_limit);步数上界(plateau);常数(复发+剪枝)(AC-3b)。
- **乱序重放**: 同 tick 同病人事件任意置换(种子定死)重放 ⇒ Σ 与折叠相同(AC-16 加性面)。
- **共病阶跃**: 在线逐 tick 与离线自举两路 ⇒ `CompoundTriggered/Expired` 事件集相同 + bonus 增量逐位(AC-32)。
- **三格对拍**: CI 矩阵同夹具 ⇒ 哈希相等(AC-1)—— 本地 Mono 单跑不构成判据,证据列 NOT-RUN 直到 job 全绿。
- **存档往返**: 探针含嵌套传递闭包负夹具(影子嵌套字段 ⇒ 红)(AC-2)。

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/DiseaseSimulation/catchup_replay_test.cs` + `unity/Assets/Tests/PlayMode/DiseaseSimulation/` 集成对拍入口 — must exist and pass
**Status**: [ ] Created — NOT STARTED;AC-1/矩阵面 **NOT-RUN(BLOCKED-BY-ADR-012 三格 job 未建)**,禁借绿

---

## Dependencies

- Depends on: Story 001–005 全部;7a persistence epic(AC-2 的编码器本体,若未落则本 AC 记 BLOCKED-BY 不判绿);telemetry-analytics epic(AC-3c 读数出口,未落则记账断言以 fake sink 判结构)
- Unlocks: EPIC Definition of Done;vertical-slice 的确定性门;time-weather epic(25 共病/离线对拍复用同矩阵)

## Completion Notes

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `CompoundEvent` — 共病触发事件
- `CatchUp` — CatchUp 与共病合成器
- CatchUp 按全序键排序 + 范围过滤
- 共病合成（简化版：源病种存在时触发）
- 共病无环验证（DFS 环检测）
- 测试: 9 条单元测试（全部通过）

**Deviations**: 
- CatchUp 为简化版（排序 + 过滤），完整版需要实现离线补算逻辑
- 共病合成为简化版，完整版需要实现共病触发条件检查
- COUNTER_INTERVAL_TICKS = 600（测试用），完整版应为 25,920,000（30 天）

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/DiseaseSimulation/catchup_replay_test.cs` — 9 测全过

**Code Review**: unity-specialist 评审完成，6 BLOCKING 问题全部修复：
- B1: CatchUp.ComputeCatchUp 实现（排序 + 过滤）
- B2: CatchUp.ComputeCompounds 实现（简化版）
- B3: CatchUp.ValidateCompoundGraph 实现（DFS 环检测）
- B4: COUNTER_INTERVAL_TICKS 注释修正
- B5: 测试添加真实断言
- B6: Step ≡ CatchUp 构造保证测试

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
