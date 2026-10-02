# Story 005: 主机权威 Append 与第二 QoS 上行 —— 客户端零 Append + 提交态归主机

> **Epic**: 玩家控制器与移动
> **Status**: Complete ✅ 2026-10-02 (实现 `e6be7ee`;判据经 `5572d66` 恢复后 6/6 通过)
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 5h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-10-02

## Context

**GDD**: `design/gdd/player-controller-and-movement.md`
**Requirement**: TR-player-006(跨格事件 `Append` 权 = 主机唯一;客户端经第二 QoS 上行其格 —— `AC-1-30` 是其唯一机械守门)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 本故事兑现系统 1 首轮 `/design-review` **第三根因的一半**(「谁 Append」原稿留空 —— EC-16 因此被整体重写;`OQ-1-9` 已裁:主机唯一 Append,客户端经第二 QoS 上行其格)。同时它是 **ADR-009 §六 世界流有界性论证在联机侧的落点**:有界性载体 = 主机(`客户端本地 Append == 0` ⇒ F-1-1b 的「≤ 1 / tick」只在主机侧需要成立)。

**ADR Governing Implementation**: ADR-020(Amendment B:客户端经第二 QoS 上行其格,`ActorCellEntered` **不在可靠通道**;Append 权 = 主机唯一)· ADR-001(第二 QoS = unreliable latest-value,按 `ActorId` 索引;pipe 抽象 `IReplayPipe` P0 预埋、P1b 实现)· ADR-005(主机唯一执行 `Step`/`Append`)· ADR-009(事件一旦进世界流,可靠有序**天然获得**,重排由三流全序键吸收)
**ADR Decision Summary**: ADR-020 Amendment B(2026-09-16 用户裁定)把 EC-16 三行表钉死:本地玩家在主机上 = 本机控制器直接 `Append`;本地玩家在客户端上 = **预测**,经第二 QoS 上行,由**主机的权威控制器** `Append`;远端队友 = 第二 QoS 上行到主机 → 主机进流 → 世界流回播全员。**不在可靠通道的三条理由**(① 最新值语义与 reliable-ordered 冲突;② 可靠通道被急救 < 50 ms 的动作同步占用,跨格事件不值得抢;③ 进流后的可靠有序由世界流天然获得)是本故事接口形状的根据,不是实现细节。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(判据本体 LOW;抬到 MEDIUM 的是 45 的 pipe 实现 = P1b,见下)
**Engine Notes**: 本故事的判据(调用点数 == 0 / 提交态归属 / 丢弃同值上行)全部是**纯 C# 断言 + AST**,不依赖任何网络库 —— ADR-001 已裁库选型(NGO / Fusion)推迟到 P1b 前 swap 评审,**刻意不等它**。P0 交付 = **模式开关 + pipe 接缝调用点 + 判据本体**;真实跨机往返 = BLOCKED-BY-45(P1b)。第二 QoS 的 latest-value 语义在 fake pipe 下可全部证伪(注入乱序 / 丢失 / 重复上行序列)。

**Control Manifest Rules (this layer)**:
- Required: **主机唯一执行 `Step` / `Append`**;客户端预测仅表现层 — ADR-005(manifest Core · 确定性模拟)
- Required: 表现态位置/格走 **第二 QoS 通道**(unreliable latest-value,按 `ActorId`),**不新增通道** — ADR-001 + ADR-020 Amendment B
- Forbidden: **判定输入类走第二 QoS**(`EmergencyAttempt` 已由 ADR-001 §一之三 裁决一改走可靠通道)—— 跨格事件是**最新值类**,两者语义分界不得混 — ADR-001 Amendment(2026-09-23)
- Forbidden: 客户端各自发世界流事件(manifest Core,ADR-016 §一「第四来源」禁则 —— `O-4` 点名 45 若实现为「客户端各自发」则被**静默击破**)
- Guardrail: `O-4`(网络拓扑登记归 45)/ `O-3` 余 45 半边(`AC-1-22` 传送频次上界)= 悬空登记项,本故事只**记账不代签**

---

## Acceptance Criteria

*From GDD `design/gdd/player-controller-and-movement.md`, scoped to this story:*

- [ ] **AC-1-30(BLOCKING · 复核新增 —— 根因 3「谁 Append」)** —— **客户端不 `Append`**:
  1 的控制器有**两种模式**(主机权威 / 客户端预测):
  ① 断言 **客户端模式下 `IEventSink.Append` 调用点数为 0**(程序集白名单 + 调用点 AST 断言);
  客户端的 `pending_cell` 只经第二 QoS 上行。
  ② **反向用例**:主机模式下同一位置样本**必须**产生 1 次 `Append`(否定"两边都不发"的空实现)。
  ③ 🔴 **提交态归主机(2026-09-16 复审新增)** —— 断言三件事同时成立:
  (a) **客户端只上行 `pending_cell`**,**不上行** `last_committed_cell`
  (AST:上行载荷的类型 / 字段集合不含已提交格);
  (b) **主机的 `last_committed_cell` 只由主机自己的 `Append` 推进** —— 注入一条与权威格**相同**的上行值,断言主机**丢弃**它(不 Append、不推进);
  (c) 注入"客户端已本地提交 `A→B`、随后回退并上行 `B→A`"的序列,断言主机侧的**格序列**中不含权威从未到达的格(否定"预测回滚插入幽灵格"的缺陷写法)。
  ⇒ **`F-1-1b` 的「≤ 1 / tick」在主机侧成立**:客户端本地 `Append == 0`,有界性载体 = 主机。
- [ ] **AC-1-22(EXTERNAL · 复核改判阻塞级 —— 原标 BLOCKING 不可能签核)** —— **传送频次上界** ——
  **本 Epic 只登记挂账,不进验收面**:主语是**外部系统**(29 复活 / 45 网络修正;25 已于 2026-09-17 S3 裁定撤销 —— 击退不注入位移)。
  ⚠️ **残缺须显式记账**:「在它落地之前,EC-4 的有界性论证只有 1 这一半」。
  29 半边已落(`death-and-respawn.md:174`/`:194-202` 登记复活传送上界);**45 半边仍悬空(45 无 GDD,P1b)= `O-3`**。
- [ ] **AC-1-04 的联机侧边界(登记,不重签)** —— `远端队友` 行(EC-16 第三行)的"全员读到同一条事件序列"由**世界流回播**保证(13 / 27 读流,不读第二 QoS 原始上行);本故事断言**回播路径存在且唯一**(第二 QoS 上行 → 主机 Append → 流回播),**不**断言 13 / 27 侧消费(归 AI Epics)。

---

## Implementation Notes

*Derived from EC-16 三行表 · R5 · ADR-020 Amendment B · ADR-001 §一之二:*

- **模式开关先立**:`SimAuthorityMode { Host, Client }`(命名建议,登记于 Completion Notes)—— 两种模式共用 story 004 的归并算子,差异只在**提交与上行**:
  - Host 模式:算子直接 `Append`(即 story 004 的单机路径,单机 == 主机模式退化形态,不得写两份算子);
  - Client 模式:算子**只更新本地 `pending_cell` 并上行**,零 `Append` 调用点。
- **上行载荷形状**:= 客户端**当前观察到的格**(`WorldPos`,整数)+ `actor_id` + 客户端 tick 观察值 —— **禁含 `last_committed_cell`**(AC-1-30③(a) 的 AST 判据面);载荷同样受 story 004 的递归类型纯净约束(AC-1-02/34 的镜像:上行载荷 ⊆ {int32, int64, 整数枚举})。
- **主机的消费路径**:收到某 `ActorId` 的上行 latest-value ⇒ 与该 actor 的**主机侧 `last_committed_cell`** 比较 ⇒ 不同则走同一归并算子提交并 `Append`;相同则**丢弃**(AC-1-30③(b))。⚠️ 主机**不做**「按上行序列重放」—— 上行是 latest-value,乱序/丢失由语义吸收(EC-16 理由①);重排的可靠有序只在**进流之后**由三流全序键保证(ADR-009)。
- **幽灵格负例是本级首要负例**:③(c) 的序列(客户端本地提交 A→B,回滚后上行 B→A)在「客户端也维护提交态」的实现形态下会让主机侧出现权威从未到达的格 —— 判据 = 主机侧格序列 == 主机视角的相邻不同格转移序列(story 004 的 AC-1-03② 序列性质判据复用,fake pipe 喂乱序)。
- **有界性的联机侧口径**:每 actor 每 tick ≤ 1 条(事件率 = **主机 tick 频率**,与玩家数无关 —— EC-16 末注)。测试 = N 个 fake actor 同 tick 各上行多次,断言主机 `Append ≤ N × tick`。
- **`AC-1-22` 挂账登记落点**:本故事的 Completion Notes / EPIC 的 Key Cross-References 各留一行「EC-4 有界性论证只有 1 这一半(45 半边悬空 = `O-3`)」。**不自签**。
- **载体注记**:45 无 GDD(P1b)⇒ 真实跨机集成测试不存在;本故事交付 **fake pipe**(可控注入乱序/丢失/重复/同值)下的全部判据。承「判据已定、载体未建」纪律。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004:归并算子本体与事件语义(EC-2/3/4/6)—— 本故事只**接**算子到两种模式,不改算子
- Story 006:`MotorSuppressed`(压制与联机模式无交互;客户端被压制时上行照发 —— 由 006 的位图语义保证)
- 系统 45:第二 QoS 通道的实现、库选型(NGO / Fusion)、重连拉流重建(ADR-001 §一之三 裁决二)—— **P1b**;`O-4` 网络拓扑登记义务归 45
- 13 / 27 对世界流的消费(各 AI Epic);远端队友的**表现态**位置插值(45 + 20 侧表现,不在 1 的流投影面)
- `AC-1-22` 的 29 / 45 侧签署(主语外部);`OQ-1-8` 剩余两条上界的裁定
- VR 模式(AC-1-04,推 P1a)

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1-30①**: 客户端模式零 `Append`(AST + 运行时计数双判据)。
  - Given: 控制器置于 Client 模式,fake `IEventSink`(计数)与 fake pipe(记录上行)。
  - When: 跑跨格序列(含 story 004 全部事件语义夹具)。
  - Then: `Append` 计数 == 0;上行计数 > 0 且载荷 == `pending_cell`;AST 断言客户端代码路径无任何 `Append` 调用点(运行期计数红不了的空路径也要被 AST 捕获)。
  - Edge cases: 模式切换 Host→Client 后调用点仍为 0(不存在「缓存的提交闭包」逃逸);Client 模式下收到**下行**世界流事件不得被本地再 `Append`(回播消费 ≠ 生产)。
  - Negative fixture: 「两边都发」实现(客户端也提交)⇒ 计数 > 0 红;「两边都不发」⇒ 由 ② 捕获(见下)。

- **AC-1-30②**: 主机模式必发(反向用例,防空实现)。
  - Given: 同一位置样本序列,模式 = Host。
  - When: 跑算子。
  - Then: `Append == 1`(与 story 004 的单元判据同夹具)—— 与 ① 的夹具**逐样本相同**,证明差异只在权威归属,不在算子。
  - Edge cases: 单机启动 = Host 退化形态(不存在第三种「无权威」模式)。
  - Negative fixture: 把 ① 的实现原样跑在 Host 下 ⇒ `Append == 0` ⇒ 本条红(两向互补)。

- **AC-1-30③(a)**: 上行载荷不含已提交格。
  - Given: 上行载荷类型经反射 + AST 双扫。
  - When: 求字段闭包。
  - Then: 含 `actor_id` / 格(`WorldPos`)/ 观察 tick;**不含**任何名为/类型为提交态的字段(判据 = 字段集合 ⊆ 白名单,非名字黑名单 —— "换个名字藏提交态"须红)。
  - Edge cases: 载荷内嵌 struct 藏 `last_committed_cell` ⇒ 递归闭包捕获(承 story 004 的 AC-1-34 同法)。
  - Negative fixture: 上行载荷加一个 committed 字段。

- **AC-1-30③(b)**: 同值上行被主机丢弃。
  - Given: 主机侧 actor 的 `last_committed_cell = A`。
  - When: 注入上行 `A`(与权威格相同),随后注入上行 `B`。
  - Then: 上行 `A` ⇒ 主机**不 Append、不推进**;上行 `B` ⇒ 提交并 `Append{B}`;主机提交态序列 == `…, A, B`(无重复 A)。
  - Edge cases: 乱序注入(`B` 后 `A` 迟到 ⇒ latest-value 语义下后到的 `A` 是否覆盖 —— 判据按 EC-16 理由①「只要最后一个格对即可」钉:主机按观察 tick/到达序取 last-write,断言不产生 `A→B→A` 的**无中生有**回访;丢失注入(`C` 从未上行,B→D ⇒ 只发 D,零中间格 —— 与 EC-4 同构)。
  - Negative fixture: 主机把上行值直接写进提交态(不比较)⇒ 同值上行也推进 ⇒ 红。

- **AC-1-30③(c)**: 幽灵格负例(预测回滚)。
  - Given: 客户端本地"已提交 A→B"后回滚,上行序列 `B, A`。
  - When: 主机消费。
  - Then: 主机侧格序列**不含**权威从未到达的格;若权威本就在 A,则序列不变(两条上行都被吸收/丢弃,零 `Append`)。
  - Edge cases: 回滚跨越多次上行(`B, C, A`)⇒ 终态仍 A 且零幽灵;回滚 + 真实前进(`B, A, C`)⇒ 只发 `C`(相邻不同格转移)。
  - Negative fixture: 实现把上行当"客户端权威提交"转发 ⇒ 序列含 B ⇒ 红。

- **联机侧有界性(AC-1-30 末注 + EC-16)**: N actor 压力。
  - Given: 4 个 fake actor(上界 = 1-4 人,P0 架构预留),同 tick 各上行多次(帧率 > tick 频率形态)。
  - When: 主机跑 ≥ 10³ tick。
  - Then: `Append 总数 ≤ actor 数 × tick 数`;事件率与玩家数曲线线性、与帧率无关。
  - Edge cases: 某 actor 断流上行(掉线中)⇒ 该 actor 零新事件,其余不受影响;actor 数 = 0 ⇒ 零事件。
  - Negative fixture: 移除主机侧比较(每条上行走算子)⇒ latest-value 高频上行使 `Append > tick` 红。

- **AC-1-22(EXTERNAL 挂账)**: 不写测试用例 —— 主语在 29 / 45;本故事只在 Completion Notes 留「EC-4 有界性论证只有 1 这一半」的登记行。**不得**以"1 侧跨格有界"冒充整条结案(禁借绿)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/player_controller/host_authority_test.cs` — must exist and pass(两模式对照 + ③(a)(b)(c) 三子断言 + N actor 有界性,fake pipe 注入乱序/丢失/重复)
- Logic: `tests/unit/player_controller/upstream_payload_test.cs` — 上行载荷字段闭包白名单(递归)

**Status**: [ ] Pending — story not yet implemented(真身落点预期 = `unity/Assets/Tests/`;登记口径 = `tests/integration/player_controller/`)
⚠️ **P0 范围内可全签**(fake pipe 判据自足);**真实跨机往返记 BLOCKED-BY-45(P1b,`O-4`)**,不得借 fake 绿冒充跨机绿。`AC-1-22` = EXTERNAL,不进本故事验收面(只挂账)。

---

## Dependencies

- Depends on: Story 004(归并算子是两模式共用的本体)/ Story 001(`IEventSink` 接缝与程序集边界);ADR-001 的 `IReplayPipe` **抽象**(P0 预埋,已由该 ADR 定形 —— 本故事只消费接口,不等实现)
- Unlocks: 45 的 P1b swap 评审(上行/消费形状 = 本故事交付的判据对像);13 / 27 的"全员同一事件序列"前提(EC-16 末注)

---

## Completion Notes

**Completed**: 2026-10-02(实现 `e6be7ee`;本行由 2026-10-02 状态回填轮补记 —— 此前 story 件长期停留 `Ready` 而代码/测试已在库,属状态漂移)
**Criteria**: AC-1-30 ①②③(a)(b)(c) 全部落地。载体 = `host_authority_test.cs` **6 例**,逐条对应:① `test_ac130_clientMode_zeroAppendCalls`(`ILBodyScanner` 扫 `OnTickEdge` 的 `Append` 调用点)· ② `test_ac130_hostMode_emitsEvent`(反向用例)· ③(a) `test_ac130_payloadExcludesCommittedCell`(载荷字段 ⊆ `{ActorId, Cell, Tick}`)· ③(b) `test_ac130_sameValueUplink_discarded` · ③(c) `test_ac130_ghostCell_noPhantomInStream` · 另 `test_boundedness_nActors`(N-actor 有界性)。**6/6 Passed**。
  ⚠️ **`SimAuthorityMode` 命名已定稿** = `{ Host, Client }`(`PlayerController.cs:27`),原 `Deviations` 的挂账条件已满足。
  AC-1-22(EXTERNAL)与 AC-1-04 联机侧边界按原文**只登记挂账**,不进验收面 —— 与 `O-3` 45 半边一致。
**Deviations**: 🔴 **判据曾被退化替换后恢复,须记账**。`185063f` 把本 story 的 6 例换成 4 例:删去 ①②③(a)(b)(c) 全部真判据,替换为 `test_clientPrediction_rollbackOnMismatch` —— 该测试**不调用任何被测代码**,只是内联重演 `if (x != y) x = y`,对 AC-1-30 是**空转判据**。`5572d66` 已恢复 `45056e6` 的 6 例版本(用户裁定方案 A)。⇒ 本 story 的「测试通过」只对**恢复后**的版本成立。
**Test Evidence**: `production/qa/evidence/` 无本 story 专项件;复跑证据 = 2026-10-02 batchmode 全量 EditMode(`total 2022 · passed 1989 · failed 0`),`HostAuthorityTest` 6/6 Passed。⚠️ **`O-4`(45 侧登记行)仍悬空** —— 45 无 GDD(P1b),未出现在任何 PR 描述中;本行不主张该义务已闭。
**Code Review**: 双代理评审修复记录见 `e6be7ee` commit message;**评审报告原文未落 `production/qa/evidence/`**。
**Manifest**: story Manifest Version 2026-09-21 = 当前 manifest(2026-09-21),无陈旧
