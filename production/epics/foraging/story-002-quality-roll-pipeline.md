# Story 002: 品级抽取管线 F-17-1(掷骰 + CDFWalk + QualityCap)

> **Epic**: 采集
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/foraging.md`(规则一/四 · F-17-1 · AC A/D 组)
**Requirement**: TR-foraging-002(gather_seq = 事件流内采集计数)· TR-foraging-003(品级掷骰的全部输入可从事件流重构,零隐藏随机态)· TR-foraging-004(CDF walk 算子以定点实现并绑 ADR-012 黄金夹具,⚠️ partial —— OQ-17-10 夹具算子未登记)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-007(主): 事件权威与掷骰状态 · ADR-005(次): 确定性模拟 · ADR-006(次): 定点域边界
**ADR Decision Summary**: 掷骰的每个输入必须可从事件流重构(核心不变量,ADR-007 ③);哈希 = `SplitMix64`,整数域,禁引擎随机(`UnityEngine.Random` / 墙钟 / 表现态任何量入公式);全部数学 Q16.16 / int64,单一舍入 `ROUND_HALF_AWAY_FROM_ZERO`;`WorldSeed` 住存档头(ADR-007 ②),`quality_distribution` 是版本化烘焙数据 ⇒ 抽取 = 三源不变量(事件流 ∪ 烘焙数据 ∪ 二者的纯函数)下的纯函数,同输入逐位同输出。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 本故事本体为纯 C# 整数算术(门 A 程序集,零引擎引用)。跨平台逐位性(AC-17-02)属 ADR-012 实测面,且该矩阵不存在 ⇒ 该 AC 标 EXTERNAL,不影响本故事的**形状**判据在 Mono 下自证。刻意不用任何 post-cutoff API。

**Control Manifest Rules (this layer)**:
- Required: `U = SplitMix64(WorldSeed, "gather", node_id, gather_seq) >> 16`(取高 48 位);`raw_quality = CDFWalk(quality_distribution, U)`,区间比较用严格 `<` 全整数;`out_quality = clamp(raw, 1, CapTable[QueryLevel(采集技能)])`;`gather_seq` 唯一来源 = 世界流 `ResourceHarvested` 的节点计数(纯函数,**零独立计数器**)
- Forbidden: `System.Random` / `UnityEngine.Random` / `Environment.TickCount` / 墙钟;`Math.Round`(ties-to-even);float/double 进入 `CDFWalk` / `SplitMix64` 路径;`raw_quality` 落流(它可重算,不存)
- Guardrail: `SplitMix64` 的中间乘法/移位 = ADR-012 F7 已登记的 IL2CPP UB 风险面 ⇒ 算子本体实现须与 item-database 既有 `Fix`/哈希件同一实现(零第二份 SplitMix64)

---

## Acceptance Criteria

*From GDD `design/gdd/foraging.md`, scoped to this story:*

- [ ] **AC-17-03**: `GIVEN` `quality_distribution` 边界(单档 100%),`WHEN` 走 `CDFWalk`,THEN `out_quality` 恒等该档,无除零 / 越界;`QualityCap` 低时夹具结论写为「恒 = 该档 ∩ ≤ Cap」
- [ ] **AC-17-04**: `GIVEN` `raw_quality > QualityCap`,`WHEN` 结算,`THEN` `out_quality == QualityCap`,且 `raw_quality` 可从 `(WorldSeed, node_id, gather_seq)` 重算(规则四)
- [ ] **AC-17-11**: `GIVEN` `gather_seq` 定义,`WHEN` 反射 / 静态检索,`THEN` 唯一来源 = `ResourceHarvested` 的节点计数,零独立计数器
- [ ] **AC-17-15**: `GIVEN` `GatherMul(L)`,`WHEN` 单测 + 边界,`THEN` `GatherMul(0) == 1` · 单调不减 · ≤ `GATHER_MUL_CAP` · `Mul` 后取整用 `ROUND_HALF_AWAY_FROM_ZERO`(防低技能双重惩罚;`GATHER_MUL_CAP` 值归 OQ-17-2 数值轮,形状判据即刻可测)
- [ ] **AC-17-02**: `GIVEN` 同一 `(WorldSeed, node_id, gather_seq)`,`WHEN` 跨平台重放,`THEN` `out_quality` 逐位相同。**⚠️ EXTERNAL · BLOCKED-BY-ADR-012** —— 三格矩阵不存在 + F7 spike 未跑 ⇒ 17 侧**不得记绿**(本仓只钉 Mono 侧自洽;夹具与刷新政策归 ADR-012 落地轮)

---

## Implementation Notes

*Derived from ADR-007 §Decision(主)/ ADR-005 / ADR-006:*

1. 抽取管线四步,全整数:① `gather_seq` = 对该 `node_id` 的 `ResourceHarvested` 前缀计数(世界流纯函数,承 Story 004 的同一前缀扫描器,一处实现);② `U = SplitMix64(WorldSeed, "gather", node_id, gather_seq) >> 16`;③ `raw = CDFWalk(quality_distribution, U)`(累计权重整数比较,严格 `<`);④ `out = clamp(raw, 1, CapTable[QueryLevel(采集)])`。
2. `SplitMix64` **复用** item-database / sim 侧既有实现(承 skill-system / item-database 已建件),禁止在 17 侧重写 —— 两份哈希 = ADR-012 F7 的两份 UB 面。
3. `CDFWalk` 是 ADR-012 夹具清单的**登记缺口**(OQ-17-10,TR-foraging-004 partial):本故事实现算子 + 单元级黄金值(Mono 侧钉死),夹具**入册动作**外抛 ADR-012 落地轮,不私签跨平台判据。
4. `raw_quality` 不落流、不快照(可重算);`out_quality` 必须存(随 `ResourceHarvested` 载荷,Story 003 落点)—— F-17-1 注。
5. `CapTable[QueryLevel]` 的 `QueryLevel` 经 30 的 `QueryLevel(采集)` 接口取(承 skill-system story-001 registry 的 int 等级口径),不读 XP 原始量。
6. 数值未裁不阻塞:`quality_distribution` 权重、`CapTable` 内容以合成夹具驱动(AC-17-25 同型纪律:形状义务不因内容未定豁免)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003:三条事件落流与 `out_quality` 的持久化载体(本故事只算,不写)
- Story 004:`EffCapacity` / `YieldDecay` 余量求值(不同公式,不同消费面)
- Story 001:表的装载校验(本故事消费装载后的纯数据)
- ADR-012 落地轮:三格 CI 矩阵、F7 spike、黄金夹具版本化刷新(golden-vN)—— AC-17-02 的执行载体,17 侧不建

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-17-03**: 单档 100% 分布恒等。
  - Given: `quality_distribution` = 仅档 q* 权重 100、余档 0 的合成表;任意 64 组 `(node_id, gather_seq)` 采样。
  - When: 跑完整 F-17-1。
  - Then: `raw` 恒 = q*;最终 `out = q*`(当 `Cap ≥ q*`)或 `= Cap`(低 Cap 侧,结论按 GDD 订正口径)。
  - Edge cases: `U = 0` 与 `U = 2^48−1` 两端点;全档权重均等;CDF 步进取舍(严格 `<`,右端点开)钉死黄金值。
- **AC-17-04**: 钳制 + 可重算。
  - Given: 构造 `raw > Cap` 的种子-节点对(穷举小空间命中);同 `(WorldSeed, node_id, gather_seq)` 二次独立求值。
  - When: 结算 + 重算。
  - Then: `out == Cap`;两次 `raw` 逐位相同;任何中间态对象上无 `raw_quality` 字段落流(反射断载荷 = registry 五字段)。
  - Edge cases: `raw = 1, Cap = 1`;`raw` 命中 `MAX_QUALITY`。
- **AC-17-11**: 零独立计数器。
  - Given: 17 的程序集。
  - When: 反射扫描字段/静态存储面。
  - Then: 无「per-node 计数器」类成员;`gather_seq` 参数只能由流前缀函数产出;向节点丢物(EC:在药丛上丢东西)不改变 `gather_seq`(非 `DropSpawned` 不计入)。
  - Edge cases: 采集失败(零事件)后再次采集 ⇒ `gather_seq` 不变(失败不消耗计数)。
- **AC-17-15**: `GatherMul` 形状。
  - Given: `L ∈ {0, 1, …, SKILL_CAP}` 全遍历,合成 `GATHER_MUL_CAP` 上下界。
  - When: 单测。
  - Then: `GatherMul(0) == MUL_ONE` · 单调不减 · `≤ GATHER_MUL_CAP` · 与 `qty_per_node` 相乘后唯一舍入点 = `ROUND_HALF_AWAY_FROM_ZERO`。
  - Edge cases: 半值 `.5` 平局远离零(区分 ties-to-even);cap 恰命中(等号过)。
- **AC-17-02**(EXTERNAL 登记用例): Mono 侧确定性自洽。
  - Given: 同输入在干净进程重跑 100 次。
  - When: 逐位比对。
  - Then: 100% 相同(本仓可证的一半);跨平台对拍**不在本故事记绿**,测试留 `EXTERNAL(ADR-012)` 标注。
  - Edge cases: 负向哨兵:输入顺序打乱(同三元组不同求值次序)结果不变(纯函数性)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Foraging/gather_quality_roll_test.cs` — must exist and pass
- EXTERNAL(AC-17-02):跨平台对拍归 ADR-012 矩阵落地轮;本故事只交 Mono 侧黄金值文件(纳入将来 golden-vN 重签)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(表装载),skill-system story-001/003(`QueryLevel` int 等级,Complete),item-database 既有 `SplitMix64` / `Fix` 件(Complete)
- Unlocks: Story 003(事件载荷消费 `out_quality`),Story 004(同一前缀扫描器扩 `gather_seq` 计数)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
