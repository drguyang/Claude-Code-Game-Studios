# Story 007: 联机分叉与远端派生(静态半)

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Integration
> **Estimate**: 5h(运行面 P1b 另计)
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/gdd/audio-system.md`(规则七 · Edge Cases 联机与空间化段 · AC-44-07/D1/D7)
**Requirement**: TR-audio-007(听感精度档 = 各设备按本机技能档,分叉的只有音)· TR-audio-008 ⚠partial(远端空间化经 `IPositionalChannel` 复用 —— 发布者实现随 45 走 P1b,**禁借绿**)· TR-audio-009(cue 载荷不复制)

**ADR Governing Implementation**: ADR-001: 联机 pipe 抽象(主:§一之二 `IPositionalChannel` + 消费纪律 + §一之三裁决二)+ ADR-018 §五(联机音频)
**ADR Decision Summary**: cue 不复制 —— 各客户端从本地流副本 + 表现映射自行派生;远端位置只读 per-`ActorId` latest-value 表,不新增通道;`SetTier(TierSource.Local)` 为默认且联机亦然(裁定 D-A)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH(ADR-001 NGO/Fusion 库面 post-cutoff,`OQ-3-x` 库 swap P1b)
**Engine Notes**: 本 story 为**静态半**:三腿中的 IL 扫描与重排差分在 P0 可跑;**4 人双实例对拍 = BLOCKED-BY-45**(P1b);`IPositionalChannel` 运行面依赖 45 未写代码(契约已定)。

**Control Manifest Rules (this layer)**:
- Required: 派生 = 流事件/sim tick 驱动(禁帧/墙钟)· `Intensity` 归一整数域 · 远端只读锚点禁判定(`ServerTick` 陈旧检查条件,ADR-001 消费纪律 2)
- Forbidden: 本地 RNG / 读表现态位置触发 cue(违 D7)· 新增 QoS 通道 · 语音通道 · 把「分叉只有音」读成可分叉病情事实
- Guardrail: P0 只交静态半;运行面对拍未跑前 D7/D1 的运行面**禁记绿**

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [ ] **AC-44-07**(静态半):`SetTier(TierSource.Local)` 为默认语义(单机 = 本地;联机亦然)—— 单元断言本地档取本机技能值;**4 人运行面对拍 = BLOCKED-BY-45**(P1b);脉案字体量分叉文档一致性核对(回归注)。
- [ ] **AC-44-D1**:空间化位置来源检查 —— 复用 `IPositionalChannel`(按 `ActorId` 索引);44 不自建位置通道/不新增 QoS/不做语音;**只读取锚点,禁读其做判定**(增益/声像/低通/距离排序 = 渲染路由可读,`ServerTick` 陈旧检查 + 允许缺省锚点;「发不发 cue」= 判定,禁)。
- [ ] **AC-44-D7**(BLOCKING,静态三腿):cue 发射派生 —— ① 输入集 ∈ {流事件, 烘焙数据} 纯函数;② **时钟面** = 流/tick 驱动,禁帧与墙钟;③ **载荷面** = DTO 全字段(除注入的 `Cell`)同纯度,`Intensity` 归一 ∈ 整数域。**三腿**:发射模块 IL 扫描(禁 `Random`/`Time`/`DateTime`/`IPositionalChannel`)· 同流重排差分哈希(含 Intensity)· 负向夹具(注入读表现态 ⇒ 红);**P1b 双实例对拍 = BLOCKED-BY-45**。

---

## Implementation Notes

*Derived from ADR-001 §一之二/§一之三 + GDD Edge Cases(2026-09-25 二轮 N1 两面扩)*:

- **两段式装配**:派生 = 是否发出 + `CueId/Intensity/Looped`;`Cell` 于空间化时注入(远端 ← `IPositionalChannel`,本机 ← 13 的 `p.Cell`)—— 「派生禁读表现态」与「DTO 携带表注入 Cell」不冲突(GDD Edge 注,回引 ADR-001 消费纪律 3)。
- 锚点时序竞争:**一次性 cue 锚点缺席 ⇒ 丢弃不补播**(与断线静默过期同语义);循环 cue 挂起至锚点到达(Edge Cases 新条)。
- 重排差分:同流 fixture 打乱到达序过 `ReorderBuffer` → cue 序列(含 Intensity)哈希一致 —— 与确定性黄金夹具(ADR-012)同族的可重构判据。
- `ServerTick` 条件是 ADR-001 已写、GDD 缺的承接(二轮 REC 落点):渲染路由读表须陈旧检查 + 缺省锚点兜底。
- 联机裁决 D-A 的反向激励论证在 GDD 规则七 —— 实现不得回退到主机档。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 008: 空间化渲染实现与 cell_jitter(本 story 只管位置来源与派生纪律)
- 45 网络层:`IPositionalChannel` 代码实现与发布者(P1b,契约已定)
- 4 人双实例对拍运行面(BLOCKED-BY-45)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-07**(静态): Given: 本机技能档值。When: `SetTier(TierSource.Local)` 初始化。Then: 档值 == 本机技能(非主机/非默认);文档核对无「取主机技能」残句。
- **AC-44-D1**: 来源纪律。Given: 44 空间化代码。When: IL 扫描 + 接口枚举。Then: 仅读 `IPositionalChannel`;无写入/无新通道方法;负向:加一个按位置做 if 分支的 cue 触发 ⇒ 注入夹具红;`ServerTick` 陈旧检查存在于读表路径。
- **AC-44-D7**: 三腿。Given: 发射模块(13/9/52/18/37/8)。When: ① IL 扫描引用白名单;② 同流 fixture 重排 → cue 序列哈希;③ 负向注入。Then: ① 零 `Random/Time/DateTime/IPositionalChannel` 引用;② 打乱前后哈希一致(含 Intensity);③ 注入读表现态触发 ⇒ 红;`Intensity` 归一输出断言 ∈ 整数域(与 `D-8-4` 已还清口径一致)。
  - Edge cases: 到达序不同但同流 ⇒ 哈希同;tick 驱动 vs 帧驱动的触发时序差异被重排腿抓住。

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `tests/unit/audio_system/net_derivation_test.cs` — 静态三腿 must pass
- 运行面(4 人对拍):**BLOCKED-BY-45**,证据待 P1b

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(DTO 护栏)· 002(事件表字段)
- Unlocks: Story 008(D1 的位置面被空间化消费);运行面解锁靠 45
