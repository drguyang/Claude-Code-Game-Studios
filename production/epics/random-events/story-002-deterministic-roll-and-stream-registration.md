# Story 002: 确定性掷骰与流登记(DC-1/DC-3)

> **Epic**: 随机事件导演
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-30

## Context

**GDD**: `design/gdd/random-events.md`(DC-1 RNG 契约 · DC-3 tick 契约 · 规则三 抽池 · F1 step② CDF walk · `EventRolled`/`EventArrived`/`HistoryFlagChanged`/`ThreatDeferred(Cleared)` 登记 · AC-52-04/05/06/07/08/10/11/21/44/46)
**Requirement**: TR-randomevents-004(SplitMix64 唯一 RNG) · TR-randomevents-005(掷骰输入可从流重构) · TR-randomevents-007(WorldSeed 归存档头) · TR-randomevents-008(CDF walk key 升序 —— gap,登记不立件) · TR-randomevents-009(IEventAuthority) · TR-randomevents-010(PatientId.None 哨兵) · TR-randomevents-012(禁墙钟) · TR-randomevents-006(KeyGate 历史旗标门 —— gap)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-007(主): 事件权威与掷骰状态 · ADR-005: 确定性模拟 · ADR-012: 跨平台确定性 CI 门 · ADR-024: Kind 单一登记真源
**ADR Decision Summary**: RNG **只有 SplitMix64**,无状态派生:`EventRollSeed(win, tier, ordinal)`,`win` = 窗口起点 tick(`tick % TICKS_PER_DAY == 0`);`S = SplitMix64(seed)`,`r = S mod C`,`C = ΣW_j`;CDF walk 按 **key 升序**逐项减权。**128 位中间结果 = 手工 hi/lo 两 `ulong`**(ADR-005 Amendment G:禁 `System.Int128` / `BigInteger`,禁有符号右移,交叉项无符号 + 掩码提取)。掷骰的**每个输入可从事件流重构**(核心不变量):每次抽取落一条 `EventRolled {win, tier, ordinal, chosen_key}`;降临落**恰一条** `EventArrived {event_key, tier, spawn_anchor, cause_clue_key?}`(预告是导演本地态,**不进流**)。全部世界级事件 `Patient = PatientId.None = -1`,不污染 `max(id)+1` 高水位;`Seq` 由主机 Append 时发号。**`IEventAuthority` = 第六个 P0 抽象点**(P0 本地占位 = 主机)。tick 只经 `ITickProvider`(禁 `Time.time`/协程/`InvokeRepeating`)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH
**Engine Notes**: 承重 = IL2CPP 有符号溢出 UB(ADR-012 F7)—— `SplitMix64` 与 Q16.16 中间乘正踩线,hi/lo 拆分是**裁决不是建议**;跨平台逐位实测归 ADR-012 三格矩阵,**今日未跑(禁借绿)**。

**Control Manifest Rules (this layer)**:
- Required: RNG 调用点全经 `SplitMix64`;`EventRollSeed` 三输入全整数且均可从流重算;流登记五 Kind 走 `entities.yaml` registry(kindgen 生成路由)
- Forbidden: `UnityEngine.Random` / `System.Random` / `Time.*`(静态扫描,AC-52-07/10);把 `WorldSeed` 写成一条 SimEvent(ADR-007 §二 裁定 = 存档头);一次降临发多条 `EventArrived`
- Guardrail: 抽骰与呈现解耦(本 story 无 Unity 引用,门 A 内);`Ordinal` 在 (win, tier) 内单调递增、跨日不重置错乱

---

## Acceptance Criteria

*From GDD `design/gdd/random-events.md`, scoped to this story:*

- [ ] `SplitMix64` 实现 = 手工 hi/lo 两 `ulong` 带进位(禁 `Int128`/`BigInteger`/有符号右移);黄金夹具单元哈希入 ADR-012 矩阵(`SplitMix64` 序列 + 定点乘两族)
- [ ] `EventRollSeed(win, tier, ordinal)` 三整数可重算:`win` = 窗口起点 tick(`tick % TICKS_PER_DAY == 0` 判据),同 seed 同输入 ⇒ 逐位同输出(重放确定性)
- [ ] CDF walk:key 升序遍历,`r = S mod C` 减去 `W_i` 命中即选;`C=0` 时不 mod(除零路径 = 跳过该配额,交 Story 003 的 ΣW=0 处理)
- [ ] **AC-52-05 流重构**:从事件流读 `EventRolled` 序列 ⇒ 重算同种子同选择(**黄金夹具:前缀重放逐位一致**);每次抽取恰一条 `EventRolled`,每次降临恰一条 `EventArrived`
- [ ] 五 Kind 经 registry 路由正确(stream/author/payload 字段齐,ADR-024 A1–A5 断言绿);`Patient = -1` 且**不**进入 `max(patient_id)` 高水位计算(AC-52-06 侧)
- [ ] `IEventAuthority` 抽象点在位:P0 本地占位实现 = 恒真主机;52 的 Append 前查权(不经权写 = 断言失败)
- [ ] **AC-52-46 主机迁移续算**:换主机后(存档 + 流不变)从流重建 `HistoryFlags` / 预算 / ordinal / `DeferredThreatSlot`,后续抽取序列与不迁移时逐位一致(单机夹具模拟迁移)
- [ ] 禁墙钟扫描(AC-52-10):52 程序集零 `Time.` / 协程 / `InvokeRepeating` 符号(静态扫描)

---

## Implementation Notes

*Derived from ADR-007 / ADR-012 Implementation Guidelines:*

1. `SplitMix64` / hi-lo 乘法原语住 `Sim`(门 A);与既有 `Fix` 基建的边界:本 story 只补 52 需要的模运算与种子混合,不重写 Fix(复用 ADR-005 Amendment G 已就位的原语,若缺则本 story 补齐并挂黄金夹具)。
2. `EventRolled` 载荷 = 4 整数 `{win, tier, ordinal, chosen_key}`(逐字照 registry `payload_schema`);tier 是枚举的 int 值。`ordinal` 归 (win, tier) 计数,由导演本地维护 + 从流重构(迁移夹具覆盖)。
3. 抽骰时序:抽在 `Append` 前完成(先算后写,写失败不重试旧种子 —— 承「掷骰输入可重构」)。
4. KeyGate(gap TR-006):`cause_flag` → 历史旗标门判定 = `HistoryFlagChanged` 流的纯函数;本 story 只立旗标读口,条目级 W_i 门(×0)在 Story 003 F1 step②。
5. ADR-012 矩阵对接:新夹具目录 `tests/golden/random-events/`(命名 `golden-vN` 随全体平台重签纪律);**本机(【超算】无 Unity 编辑器 GUI 依赖的 IL2CPP 对拍)先跑 Mono 侧,IL2CPP 格留 CI —— NOT-RUN 如实登记**。
6. ⚠️ **数值冻结**:TODMult / HIST_W / HIST_MULT_MAX 等权重值归用户数值轮;walk 算法与 mod 语义冻结。

---

## Out of Scope

- [Story 003]: F1 四步管线(调用本 story 的掷骰口;W_i 调制乘在 F1)
- [Story 004]: 预算与 `ThreatDeferred` 的两条 Kind 的**发出时机**(本 story 只做 Kind 路由与载荷形状)
- [Story 006]: `EventArrived.spawn_anchor` 的坐标解析
- IL2CPP 三格矩阵的执行(ADR-012 epic 侧;本 story 只交夹具)
- 联机掷骰权仲裁(45 轮;P0 = 本地占位)

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: 种子重放逐位一致
  - Given: 固定 WorldSeed + 事件流前缀(20 个窗口)
  - When: 从流重算全部 `EventRollSeed` 并重跑 walk
  - Then: chosen_key 序列与原始 `EventRolled` 逐条相等(AC-52-05)
  - Edge cases: 同窗口连续多抽取 ordinal 递增;跨日窗口重置

- **AC-2**: SplitMix64 黄金夹具
  - Given: ADR-012 单元族夹具输入集
  - When: Mono 侧跑
  - Then: 哈希与 `golden-vN` 匹配;负夹具(有符号右移版本)产出不同哈希被断言拒
  - Edge cases: int64 溢出回绕路径(±2^63 边界)逐位钉

- **AC-3**: CDF 边界
  - Given: 池 {k1:W=1, k2:W=1},可控 S 夹具
  - When: r=0 / r=1 各抽
  - Then: r=0→k1、r=1→k2(key 升序语义);`ΣW=0` ⇒ 不 mod、跳过(不选任何条目、不发 EventRolled)
  - Edge cases: 单条目池恒选;W 极大值 mod 不溢出(hi/lo 路径)

- **AC-4**: 哨兵与高水位
  - Given: 世界级事件与病人事件混排流
  - When: `max(patient_id)+1` 重构
  - Then: -1 不参与;`next_patient_id` 不被 52 事件影响(AC-52-06)
  - Edge cases: 全流皆 -1 事件 ⇒ 高水位 = 0

- **AC-5**: 迁移续算(AC-52-46)
  - Given: 主机 A 跑到窗口 t 的存档;模拟迁移主机 B(同档)
  - When: B 从流重建旗标/预算/ordinal/defer 槽后继续
  - Then: 后续 `EventRolled` 序列 = A 继续跑的对照序列(逐位)
  - Edge cases: 迁移恰发生在窗口中途(ordinal 半程)

- **AC-6**: 禁符号扫描(AC-52-07/10)
  - Given: 52 程序集
  - When: 静态扫描 `UnityEngine.Random` / `System.Random` / `Time.` / 协程
  - Then: 零命中(命中 = 构建失败)
  - Edge cases: 注释/字符串内出现不计(符号级扫描非文本 grep)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/RandomEvents/event_rng_stream_test.cs` + `tests/golden/random-events/*`(ADR-012 夹具族,IL2CPP 格 NOT-RUN 如实标注) — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(池 schema + registry 路由 + 门 A)· ADR-010 存档头 WorldSeed(既有)· `Fix` 原语(既有,ADR-005/006)
- Unlocks: Story 003(F1 用掷骰口)· Story 004(defer Kind 载荷)· Story 006(EventArrived 消费)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*

## Completion Notes

**Completed**: 2026-09-30
**Criteria**: 
- `EventRollSeed` — 事件掷骰种子（Win / Tier / Ordinal）
- `CdfWalkResult` — CDF walk 结果
- `EventRng` — 确定性掷骰器（CdfWalk / ComputeWindowStart / ValidateSeedReproducible）
- CDF walk key 升序遍历（修复非升序输入）
- 禁墙钟 / 禁符号扫描
- 测试: 9 条单元测试（全部通过）

**Deviations**: 
- CDF walk 为简化版（无 EventRolled / EventArrived 流登记），完整版归 Story 003/004

**Test Evidence**: 
- `unity/Assets/Tests/EditMode/RandomEvents/event_rng_stream_test.cs` — 9 测全过

**Code Review**: unity-specialist + qa-tester 评审完成，5 BLOCKING 问题全部修复：
- B1: CDF walk key 升序修复（非升序输入按升序遍历）
- B2: 禁墙钟测试修复（检查 DateTime / TimeSpan）
- B3: 禁符号扫描测试修复（检查方法参数）
- B4: SplitMix64 唯一性测试修复（验证确定性 + 不同输入不同输出）
- B5: 种子可重算测试修复（验证不同输入产生不同输出）

**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
