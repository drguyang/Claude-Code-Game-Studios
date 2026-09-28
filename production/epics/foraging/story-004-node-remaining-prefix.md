# Story 004: 节点余量前缀函数 F-17-3 + 采前闸

> **Epic**: 采集
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/foraging.md`(F-17-2 / F-17-3 · 规则三 · Edge Cases 满载/余量行 · AC B/D 组)
**Requirement**: TR-foraging-006(资源节点余量 = 前缀函数,历史采集事件序列的整数纯函数,⚠️ partial —— OQ-17-5 现算 vs 缓存未裁)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主): 派生态判据(余量 = 事件流前缀的纯函数,不进流不存档)· ADR-005(次): 主机唯一求值 · ADR-006(次): 定点乘除与唯一舍入点
**ADR Decision Summary**: 余量是**派生态**,由 `ResourceHarvested` 前缀 ∪ 烘焙节点初始容量确定性重建(ADR-009 §四 两类源判据:事件流 + 版本化烘焙数据);季节乘子 `SEASON_MULT[season_index(t)]` 的 `season_index` 归 5 的 F-5.2(消费不重定义);采前闸「`remaining ≥ Fix(qty)` 才允许发事件」由**主机在裁决时求值**(OQ-17-6 未裁 ⇒ P0 单机主机=本地);`qty = Mul(Fix.FromInt(qty_per_node), GatherMul(L))` 唯一舍入点(承 Story 002)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数定点算术(Q16.16 `Fix` 乘除、先乘后移位、hi/lo 中间量),零引擎 API;`Fix` 乘法的 IL2CPP 逐位面归 ADR-005/012 既有实测义务,不在本故事新增。

**Control Manifest Rules (this layer)**:
- Required: `EffCapacity = BaseCapacity × SEASON_MULT / MUL_ONE × YieldDecay(Σ_all qty@node)`;`YieldDecay(n) = max(DECAY_FLOOR, MUL_ONE − Mul(n, DECAY_RATE))` 单调不增;`remaining = max(0, EffCapacity − Fix(Σ_window qty))`;采前闸 = 主机裁决时判 `remaining ≥ Fix(qty)`,不足 ⇒ **零事件**;缓存(若采缓存实现)键必须是纯 `(流前缀, tick)` 且从空重建等值(OQ-17-5 约束面)
- Forbidden: 余量/容量落流或落独立快照(派生态纪律);窗口 Σ 用浮点累加;把「在场才模拟」读成「全域模拟再 LOD」的反向违规不适用(17 无此面);客户端各自判余量出第二真源
- Guardrail: `RegrowWindow ≤ 0` 装载期硬失败已归 Story 001,本故事以「窗口参数合法」为前提只做求值;数值(`DECAY_RATE`/`DECAY_FLOOR`/`SEASON_MULT` 内容)归用户数值轮,形状与单调性即刻可测

---

## Acceptance Criteria

*From GDD `design/gdd/foraging.md`, scoped to this story:*

- [ ] **AC-17-06**: `GIVEN` `remaining(node, t) < Fix(qty)`,`WHEN` 发起采集,`THEN` 零事件(不发 `ResourceHarvested` / 成长)(F-17-3 求值 ②)
- [ ] **AC-17-17**: `GIVEN` `YieldDecay(n)`,`WHEN` 单测,`THEN` 单调不增 · `YieldDecay(0) == MUL_ONE` · 有下限(≥ `DECAY_FLOOR`)(深水线「衰减」半截的机械证据)
- [ ] **AC-17-13**: `GIVEN` 同种子同节点、两个不同 `QueryLevel`,`WHEN` 差分夹具比对 `out_quality`,THEN 高技能者 ≥ 低技能者(差分自动,ADR-019 回放即记录)。**⚠️ BLOCKED-BY-OQ-17-2** —— `GATHER_MUL_CAP` 等值未裁 ⇒ 差分**形状夹具先行**,真实表数字待数值轮,不得以合成单调表冒充真实平衡
- [ ] **AC-17-14/15 邻接(前缀一致性)**: `gather_seq` 计数与余量窗口 Σ 扫的是**同一条 `ResourceHarvested` 前缀**,一次扫描两处消费(零分叉实现);从空流重建 `remaining` 与增量维护逐位相同

---

## Implementation Notes

*Derived from ADR-009 §四(主)/ ADR-006:*

1. 单一前缀扫描器:对世界流按 `node_id` 过滤 `ResourceHarvested`,一次遍历同时产出 ① `gather_seq`(计数)② `Σ_all qty`(喂 `YieldDecay`)③ `Σ_window qty`(近 `RegrowWindow` 内,喂 `remaining`)—— 一处实现三消费(AC-17-11 / TR-foraging-002 与本合同一条码)。
2. F-17-2 的 `qty` 计算落本故事:`Mul(Fix.FromInt(qty_per_node), GatherMul(L))` 唯一舍入 `ROUND_HALF_AWAY_FROM_ZERO`;`GatherMul` 曲线归 Story 002 已建,本故事只调用。
3. 采前闸时序:主机收到采集意图(经 Story 005 的 action 路径)→ 先判 `remaining ≥ Fix(qty)` → 不足 ⇒ 回「余量不足」具名失败(零事件);足 ⇒ 掷骰(Story 002)→ 发三条(Story 003)→ 成长。打断/失败**不消耗**余量(零事件即零消耗,结构上保证)。
4. `season_index(t)` 从 5 的世界时间读数取(tick 域,20 Hz ⇒ `TICK_SECONDS = 0.05` 已标定);表长校验归 Story 001;本故事只做乘法。
5. OQ-17-5(现算 vs 缓存)未裁 ⇒ 实现取**现算**为默认(最小正确),留 `IRemainingProvider` 接缝;若实现期改缓存,须满足「键 = 纯 (流前缀, tick) + 从空重建等值」并回填 OQ-17-5 —— 该约束写进代码注释与测试,不私裁。
6. 在药丛上丢东西(`DropSpawned` 非采集来源)不污染 `gather_seq`、也不计入窗口 Σ(Edge Case,过滤谓词只认 `ResourceHarvested`)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001:`RegrowWindow` / `SEASON_MULT` 装载校验(本故事消费合法值)
- Story 002:掷骰与 `GatherMul` 曲线本体
- Story 003:三条事件的发号与铸造(本故事只决定「发不发」)
- Story 005:失败三类反馈的呈现接线(本故事只产「余量不足」具名失败枚举值)
- 5 时间天气:`season_index` 计算(消费其 F-5.2,不重定义)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-17-06**: 余量不足 ⇒ 零事件。
  - Given: 合成流使某节点 `remaining = Fix(1/2) < qty = 1`;节点 `remaining = qty` 恰等界。
  - When: 发起采集。
  - Then: 不足例:spy-sink 零事件、零成长、余量不变;恰等例:成功(`≥` 取等)。
  - Edge cases: `remaining = 0`(枯节点);`EffCapacity` 被负窗口 Σ 抬高 ⇒ `min` 封顶不越 `EffCapacity`(窗口定义钉死)。
- **AC-17-17**: YieldDecay 三性质。
  - Given: `n ∈ {0 … 200}` 全遍历,合成 `DECAY_RATE` / `DECAY_FLOOR`。
  - When: 单测。
  - Then: `D(0) == MUL_ONE`;`D(n+1) ≤ D(n)`;`D(n) ≥ DECAY_FLOOR`(下界防归零)。
  - Edge cases: 大 n 截断到 floor 后恒等;`DECAY_RATE` 极小 ⇒ 长尾单调不破。
- **AC-17-13**(BLOCKED-BY-OQ-17-2 半边): 技能差分的形状。
  - Given: 同 `(WorldSeed, node_id, gather_seq)`,`L1 < L2`;合成单调 `GatherMul`(标注「形状夹具非真实表」)。
  - When: 跑 F-17-1/F-17-2 全链。
  - Then: `CapTable` 单调保证 raw→out 上界不降;`qty(L1) ≤ qty(L2)`(唯一可比对的形状半)。真实平衡差分值待数值轮回填后翻真绿。
  - Edge cases: 两 L 同段(Cap 相同)⇒ 允许等号;夹具表恒标 `SHAPE_ONLY`。
- **前缀一致性**: 重建等值。
  - Given: 随机(固定种子)事件序列 500 条;两种路径:a) 全量现算 b) 增量维护后清空重建。
  - When: 每 tick 比对 `remaining` / `gather_seq`。
  - Then: 逐位相同(long 相等)。
  - Edge cases: 多玩家同 tick 串行插入(顺序由全序键定)⇒ 两路径同序。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Foraging/node_remaining_prefix_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(合法表值),Story 002(`GatherMul`/`qty` 公式件)
- Unlocks: Story 005(端到端裁决链把采前闸接入动作路径;三类失败之一的判据源)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
