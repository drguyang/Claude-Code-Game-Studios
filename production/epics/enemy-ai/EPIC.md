# Epic: 敌人 AI

> **Layer**: Foundation → Core → Feature
> **GDD**: design/gdd/enemy-ai.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)+ 呈现消费侧(42/44)
> **Status**: Ready
> **Stories**: 5 stories — see table below

## Overview

敌人 AI(27)是**分层确定性**的样板系统:行为**决策进 sim**(整数/定点域,可重放),**运动表现态**(连续位移/动画/绕障)在表现层驱动(ADR-016 §一/§二)。全部决策输入**恰三源** —— 世界流事件(`ActorCellEntered` 的粗粒度格,半格盲区是设计不是缺陷)· 版本化烘焙数据(手写 `ai_enemy.json` 经 ADR-014 两阶段管线烘 `*.cooked`,零行为树工具、零第三方库)· 二者的纯函数;任何第四来源即静默不可重建通道。六态最小机 `{Patrol, Alert, Chase, Flank, Engage, Disengage}`,`Down` **不是**第七态(= 9 的归零真值投影);士气/脱离/决策频率/步进/A* 全部整数量(F-27-1…7),冻结判据只用 sim 量(`d2`/`in_combat`,「离屏」已删),`acc`/`path_cursor` 等积分量冻结须同冻。单套行为程序 × 兵痞/野兽两类参数行 —— `entity_kind`/`down_class` 是身份不是分支,只落输出侧。敌人复用 9 的伤情模型(归零=INJ_COMA 非致命,支柱二实现侧),id 经 `IIdAuthority` 与病人共空间,敌人行住**世界流**不折叠;可写 Kind 集恰 = {`EncounterEnded`},且**永不写第二条 `ActorCellEntered`**。呈现侧只交付整数语义 `EnemySignalDto` 与 cue 触发(读数条归 42、音归 44)。全部逐参数数值(OQ-27-1…7)归用户数值轮,机制与合法性断言先行。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-016: AI 架构(主) | 分层确定性:决策进 sim、位移表现态;三源不变量;粗粒度整数格感知禁读 Transform;载体=作者期数据表→构建期烘整数(2026-09-17 修订:零可视化工具);§九 冻结只用 sim 量 + 积分量同冻 | MEDIUM |
| ADR-005: 确定性模拟 | 全部数学整数定点(Q16.16/int64);主机唯一执行 Step;同 tick actor_id 升序读上 tick 快照 | HIGH |
| ADR-006: 定点边界契约 | FixParse 唯一入口(禁浮点字面量);ROUND_HALF_AWAY_FROM_ZERO;Amendment B id 高水位 `max(id)+1` 扫三流并集 | MEDIUM |
| ADR-009: 世界状态事件化边界 | 决策=派生态不进流、效果进流;敌人行走世界流;全序键 `(Tick, StreamPriority, Patient, Seq)` | MEDIUM |
| ADR-010: 持久化 | 三流序列化/折叠谓词**不适用敌人行**;高水位由事件流重构 | MEDIUM |
| ADR-012: 跨平台确定性 CI 门 | A*/士气/步进逐位一致挂黄金夹具三格矩阵(AC-27-01 EXTERNAL);int64 回绕 vs IL2CPP UB(§F7) | HIGH |
| ADR-014: 数据管线 | `ai_enemy.json` 两阶段烘焙;陈旧门=构建失败;Fix 字段 JSON 写字符串 | MEDIUM |
| ADR-015: 世界几何 | `WorldPos=(i32,i32,i32)` 单一整数格;导航格由关卡工具切片;NavMesh 仅表现 | LOW |
| ADR-017: DOTS 裁决 | sim 侧结构性排除(门 A 引用集恰=BCL+Sim.Contracts);本 epic 零 DOTS | LOW |
| ADR-018: 音频边界 | cue 走 `AudioCueDto` 同构、行为反馈白名单;27 只触发不持有 | MEDIUM |
| ADR-020: 玩家控制器 | `ActorCellEntered` 写方=系统 1(27 是读方);事件率上界=tick 频率 | LOW |
| ADR-021: POI 状态所有权 | 写权=状态所有权分工:`EncounterEnded` 归 27 的判据来源 | LOW |
| ADR-022: 关卡工具 | `world_nav_{chunk}` 导航格切片产出方;一致性检查 C1–C6(CI EXTERNAL) | MEDIUM |
| ADR-024: Kind 单一真源 | `EncounterStarted`/`EncounterEnded` 经 `entities.yaml`+kindgen 登记;27 可写集白名单落点 | LOW |

**Engine Risk**: **MEDIUM**(承 ADR-016)—— sim 侧判据纯 C#(LOW);抬到 MEDIUM 的仅剩表现层一处(动态建造下的 NavMesh tile 更新成本,实现期 profile);跨平台逐位对拍挂 ADR-012 CI 矩阵(EXTERNAL,本机不可跑时记 NOT-RUN,禁借绿)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-enemy-*`,Manifest Version 2026-09-21)。
> 计数(实测):**21 条 = 19 covered + 2 partial**;partial = TR-004(`Down` 非态的另一半 = 9 真值路由,归 Story 005 落)/ TR-005(单程序×两参数行的运行期等价验证,归 Story 001 落)。本 epic 五个 story 完成后两条 partial 转 covered。
> GDD AC 共 **35 条 = 33 BLOCKING + 2 ADVISORY**(AC-27-33/34);A6·B9·C6·D7·E7 五组各归恰一个 owning story。

| TR-ID | Requirement(摘) | ADR Coverage |
|-------|-------------|--------------|
| TR-enemy-001 | 决策输入恰三源;第四来源 = Forbidden Pattern | ADR-016+009 ✅ |
| TR-enemy-002 | 感知 = 最近 `ActorCellEntered` 的粗粒度格,禁读 `Transform.position` | ADR-016+020 ✅ |
| TR-enemy-003 | 格距整数平方和禁 sqrt;视线 Bresenham 逐格查,禁 Raycast | ADR-016+006 ✅ |
| TR-enemy-004 | 六态最小集;`Down` 不是第七态 | ADR-016 partial(story 003/005) |
| TR-enemy-005 | 单套行为程序 × 两类参数行,零 `isBeast` 分支 | ADR-016 partial(story 001) |
| TR-enemy-006 | 载体 = `ai_enemy.json → .cooked`,运行期零第三方行为树/寻路库 | ADR-016+014 ✅ |
| TR-enemy-007 | 禁 float:空间量整数格/朝向枚举,非空间量 Fix | ADR-016+006 ✅ |
| TR-enemy-008 | 决策 = 派生态不进流,效果进流 | ADR-016+009 ✅ |
| TR-enemy-009 | LOD 节流(近/远/冻结),解冻三源重建逐位一致 | ADR-016 ✅ |
| TR-enemy-010 | 「可脱离」是对等态,进入条件并列两路,禁硬锁定仇恨 | ADR-016 ✅ |
| TR-enemy-011 | 寻路输入 = 23 的 `EffectiveWalkable`,不读 `Nav` 原件 | ADR-016+015 ✅ |
| TR-enemy-012 | 重规划由 27 轮询触发,不自建 blocked 副本 | ADR-016 ✅ |
| TR-enemy-013 | sim 整数 A*,NavMesh 仅表现,漂移单向 snap | ADR-016+015 ✅ |
| TR-enemy-014 | `LogiPose` 逐 tick 整数推进,禁反推格 | ADR-016+015 ✅ |
| TR-enemy-015 | 敌人复用 9 的伤情模型,不新开第二套生命 | ADR-016 ✅ |
| TR-enemy-016 | id 经 `IIdAuthority` 与病人共空间/高水位 | ADR-006+010+016 ✅ |
| TR-enemy-017 | 伤情事件落世界流;载荷形状归 25 | ADR-016+009 ✅ |
| TR-enemy-018 | `ITameable` P0 定型不实现,命令通道复用行为程序 | ADR-016 ✅ |
| TR-enemy-019 | 遭遇两 Kind 写者拆开:Started=52 / Ended=27 | ADR-016+021 ✅ |
| TR-enemy-020 | 27 不写第二条 `ActorCellEntered`(敌人格纯派生态) | ADR-016+009 ✅ |
| TR-enemy-021 | 27 不做读数条/音效/不持 UI,只交付触发与信号 | ADR-013+018+016 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/enemy-ai.md`(AC-27-01…35,33 BLOCKING + 2 ADVISORY)are verified —— 含 EXTERNAL 对拍(AC-27-01 挂 ADR-012 CI 矩阵)与 `[A]` 两条(AC-27-33/34)
- TR-enemy-004 / TR-enemy-005 两条 partial 随 Story 001/003/005 落地转 covered(registry 回写,禁提前)
- All Logic and Integration stories have passing test files in `tests/`(或 `unity/Assets/Tests/`)
- 数值轮解冻前,机制与合法性断言全部就位;OQ-27-1…7 的定值不作为本 epic 的阻塞项(OQ-27-7 的 `NODE_BUDGET`/`ENCOUNTER_TIMEOUT` **量级**为写 A*/Story 005 前置,已在故事内登记)

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 行为程序载体 —— ai_enemy.json 烘焙 schema 与整数决策器 | Logic | **Complete ✅ 2026-09-30** | ADR-016/014/006 |
| 002 | 感知与目标 —— 三源白名单、Band 分档互斥、整数视线与 Target(e) | Logic | **Complete ✅ 2026-09-30** | ADR-016/020/005 |
| 003 | 敌意状态机 —— 六态转移、士气/脱离单一出处与 LOD 节流 | Logic | Ready | ADR-016/005/006 |
| 004 | 确定性移动与寻路 —— 定点累加器步进、整数 A* 与路径重规划 | Logic | Ready | ADR-016/015/022 |
| 005 | 遭遇生命周期、伤情真值路由与呈现信号契约 | Integration | Ready | ADR-016/021/024/013/018 |
