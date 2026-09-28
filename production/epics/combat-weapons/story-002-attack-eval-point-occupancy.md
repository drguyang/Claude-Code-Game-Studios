# Story 002: 攻击求值点:意图通道 · 占用门 · tick 内次序

> **Epic**: 格斗与武器线
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28
## Context

**GDD**: `design/gdd/combat-and-weapon-lines.md`(规则〇 意图→求值点 · 先判后打 · 占用门三边界 · tick 内次序 · §〇 组 AC)
**Requirement**: TR-combat-007(占用门 Occupied(a) = 事件流求值的纯函数;t+cooldown_ticks 清零;压制不占用 / Natural 占用 / 占用不发事件不排队) · TR-combat-022(27→25 有入向意图、无出向数值;规格依赖非运行时接口) · TR-combat-023(20 Hz tick 已裁,TICK_SECONDS=0.05)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主): 确定性模拟;ADR-011: 输入架构
**ADR Decision Summary**: t = sim tick(`ITickProvider`,20 Hz,不由渲染帧驱动);tick 内固定次序 = 先吸收 `ActorCellEntered` → 攻击求值 → onset `Append`(AC-25-1-08:Step ≢ CatchUp 的次序面);Attack 走 3 的普通动作回调路径(非急救直读通道,R19b 已回填 3 的动作表);意图**不排队不缓冲** —— 求值点没有「下一帧再打」的语义。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 逻辑本体纯 C#(LOW);MEDIUM 来自 Input System 6.3 的 Attack 动作行为须实测(归 3 的 epic),25 侧只消费聚合后的意图信号。

**Control Manifest Rules (this layer)**:
- Required: 求值点收两类入向意图 —— 玩家的 Attack 动作(经 3)与 27 的 Engage 意图;占用判定 `Occupied(a)` 由事件流 + cooldown 派生(无独立状态)
- Forbidden: S-1 结构体中出现 `Queue`/`List` 缓冲(AC-25-0-04 类型扫描);占用态写任何事件;为「先判后打」引入队列语义
- Guardrail: 占用只影响**出手可否**,不影响 onset 内容;压制(suppression)与占用互不相干(压制不占用)

---

## Acceptance Criteria

*From GDD `design/gdd/combat-and-weapon-lines.md`, scoped to this story:*

- [ ] AC-25-0-01:同一 actor 在 cooldown 窗口内的第二次意图被**丢弃**(非排队、非缓冲),窗口释放后新意图可执行
- [ ] AC-25-0-02:压制中的 actor 出手**不被占用门拦**(压制不占用)
- [ ] AC-25-0-03:Natural(兽)攻击照常占用(Natural 占用)
- [ ] AC-25-0-04:类型扫描断言攻击求值路径无 Queue/List 缓冲字段
- [ ] AC-25-0-05:占用释放后不发任何事件(占用不发事件);占用态重放可由 (事件流, cooldown_ticks) 逐位推出
- [ ] tick 内次序断言:同一 tick 同时到达 `ActorCellEntered` 与攻击意图时,先更新格、后求值命中(可复现的求值快照)
- [ ] 27 的 Engage 意图入向为**规格依赖**(共读烘焙动作表),25 不暴露运行时回调接口给 27

---

## Implementation Notes

*Derived from ADR-005 Implementation Guidelines:*

1. `Occupied(a)@t := ∃ onset 事件 o(o.actor=a ∧ o.tick ≤ t < o.tick + cooldown_ticks[act])` —— 纯函数读流,无字段。
2. 求值点入口签名:`OnAttackIntent(actorId, actionId, tick)` → 判占用 → 交 Story 003 命中判定 → 交 Story 006 onset Append。
3. tick 内次序由 tick driver(ADR-023 Boot 场景)统一编排:世界流格事件先行灌入,再逐 actor 求值(确定性序 = actor id 升序,禁字典迭代序)。
4. 意图信号来源:玩家 = 3 的 Attack action started 回调(边界层,非 sim);27 = sim 内 AI 决策直接产出意图结构。两路在求值点合流,之后路径同构。
5. 白名单/Down 等合取项在 Story 003;本 story 只管「能不能进入求值」(占用 + 时序)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 003]: F-25-1 命中六合取项(d2/解锁/白名单/Down)
- [Story 004]: magnitude 计算
- [Story 005]: 切换冷却与压制的读写面
- [Story 006]: onset 事件载荷与流写入
- 系统 3:Attack 动作资产与绑重;系统 27:Engage 决策产生

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(丢弃非排队)**: cooldown 窗口内二次意图
  - Given: actor A 在 t₀ 出手(cooldown=4)
  - When: t₁=t₀+1 注入第二意图;窗口后 t₀+4 注入第三意图
  - Then: 第二意图被丢(流中无对应 onset、无缓冲);第三意图正常求值
  - Edge cases: 恰 t₀+4(释放边界)按「< 不放、≥ 放」判据(与 story-005 共用谓词)
- **AC-2(压制不占用)**: 被压制者出手
  - Given: actor B 处于压制(duration 窗口内)
  - When: B 发意图
  - Then: 不因压制拦下;若 B 的占用窗已满则照常命中并产生 onset
  - Edge cases: 压制刷新(max 不 sum)不改变占用判定
- **AC-3(无缓冲断言)**: 类型扫描
  - Given: 攻击求值路径的类型集
  - When: 反射扫描字段类型
  - Then: 无 `Queue<>`/`List<>` 意图缓冲
  - Edge cases: 局部集合(遍历表)不违令 —— 扫描面 = 结构体字段

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Combat/combat_attack_eval_point_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(cooked 动作表);系统 3 的 Attack 动作信号(未就绪时以意图注入桩联测,BLOCKED 标注不记绿);`ITickProvider` 抽象点(已立)
- Unlocks: Story 003(命中判定被本求值点调用)、Story 006(onset 写入)

---

## Completion Notes

*(留空 — story 关闭时回填)*
