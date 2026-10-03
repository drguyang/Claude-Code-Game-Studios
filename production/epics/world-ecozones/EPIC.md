# Epic: 世界与生态区

> **Layer**: Foundation → Core → Feature
> **GDD**: design/gdd/world-and-ecozones.md
> **Architecture Module**: L2 Sim(门 A 侧 · SIM)+ Tooling 消费侧(ADR-022 关卡工具)
> **Status**: Complete ✅ 2026-10-03(6/6 story 已实现;两轮评审的判据缺陷 N1/N5 已修并复跑 **109/109 绿 0 红**;评审原件两份已落 `qa/evidence/`;未闭项 = **N3** 白名单判据待 27 侧落地 + **Story 005** [L] 走查 EXTERNAL —— 详见 §Epic Status)
> **Stories**: 6 stories — see table below

## Overview

世界与生态区(6)是 P0「一个医馆 + 一个小场景」的**几何与状态所有者**(开放世界是 P1a 愿景,不在本 epic)。它拥有三件事:① **单一整数世界格** `WorldPos=(i32,i32,i32)` —— 地形/建造槽位/掉落锚点/导航格/医馆房间格共用同一套格(ADR-015 §三),格大小与速度上限的防隧穿关系 `LATTICE_SIZE ≥ SPEED_MAX×MAX_DT×SAFETY_MARGIN`(F-6-1)与 `K_TERRAIN_MAX = max K_speed`(F-6-2,空表硬失败)全在整数域;② **EcozoneOf(cell) 整数几何查询**(F-6-3:开集 + 正则化 + 边界 min(id) 仲裁,射线法全整数,`NONE=-1` 哨兵,y 不参与);③ **POI 状态的一分为二** —— 定义 = 派生态(烘焙逻辑层,不进流),状态 = 模拟态(`enum PoiState{Undiscovered,Discovered,Resolved}` 三态单调不可逆、可跳过,R-6-7),唯一写通道 `PoiStateChanged`(写者=6、主机唯一 Append,R-6-9),转移总数有界 `≤ 2×|POI_DEF|`(F-6-4),52 的 `spawn_anchor` 只读定义不读状态(R-6-10)。运行期 chunk 激活权 = 6,只读 `ActorCellEntered` 的格(派生态不进流,ADR-023 ⑥)。全部逻辑层数据由 ADR-022 关卡工具作者产出、经 ADR-014 烘焙;6 是**消费者不是生成器**。数值(LATTICE_SIZE / K_speed / slopeLimit)归用户数值轮。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-015: 世界几何 | 单一整数格;手工烘焙固定世界;派生态两类源(种子纯函数/版本化烘焙数据);视觉层采样禁入 sim | LOW |
| ADR-021: POI 状态所有权 | 定义派生/状态模拟;所有者+唯一写者=6;PoiStateChanged Kind;有界性 ≤ \|POI\|×\|STATE\|;spawn_anchor 读定义 | LOW |
| ADR-009: 世界状态事件化边界 | 三问判据/三态分类;世界流不折叠+有界性;全序键 (Tick,StreamPriority,Patient,Seq) | MEDIUM |
| ADR-005: 确定性模拟 | 主机唯一执行 Append/Step;整数定点域 | HIGH |
| ADR-006: 定点域边界 | 载荷整数枚举,禁 float/字符串;FixParse 边界 | MEDIUM |
| ADR-014: 数据管线 | world_*.json → .cooked 两阶段烘焙;装载期硬失败 | MEDIUM |
| ADR-022: 关卡工具 | 逻辑层整数数据唯一作者;C1–C6 一致性检查(工具侧,不进构建) | MEDIUM |
| ADR-023: 场景生命周期 | chunk 激活权=6;World 场景零 gameplay GameObject;只读 ActorCellEntered 的格 | HIGH |
| ADR-020: 玩家控制器 | ActorCellEntered 写方口径(跨格发事件、事件率上界=tick 频率)——6 是读方 | LOW |
| ADR-024: Kind 单一登记真源 | PoiStateChanged 已具名入 entities.yaml(stream/author/payload_schema 必填) | LOW |

**Engine Risk**: **MEDIUM**。6 本体 = 纯整数几何 + 事件写出(门 A,LOW);抬到 MEDIUM 的是烘焙装载面(ADR-014 Addressables 6.2+ 启动期硬失败为 post-cutoff)与 ADR-023 场景/chunk 实装(EXTERNAL 项挂 CI 矩阵)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-worldeco-*`,Manifest Version 2026-09-21)。
> 计数(实测):**10 条全部 covered**(ADR-021/015/009/023/007)。
> AC-6-25/26 为 EXTERNAL(挂关卡工具 CI 与数值轮),AC-6-24 为 [L] 走查 —— story 拆解照登不借绿。

| TR-ID | Requirement(摘) | ADR Coverage |
|-------|-------------|--------------|
| TR-worldeco-001 | POI 定义 = 派生态,烘焙逻辑层加载重建,不进流 | ADR-021+015 ✅ |
| TR-worldeco-002 | POI 状态 = 模拟态;所有者与唯一写者=6;主机唯一 Append | ADR-021 ✅ |
| TR-worldeco-003 | PoiStateChanged 载荷 {poi_id,new_state} 均整数枚举 | ADR-021+009 ✅ |
| TR-worldeco-004 | 世界级事件 Patient=PatientId.None,不污染高水位 | ADR-021+007 ✅ |
| TR-worldeco-005 | 状态变更全经流,无第二存储(存档字段/场景对象/内存标志均禁) | ADR-021 ✅ |
| TR-worldeco-006 | 重放/迁移后状态从世界流重建,快照非真源 | ADR-021+009 ✅ |
| TR-worldeco-007 | 状态转移总数有界 ≤ \|POI\|×\|STATE\| | ADR-021+009 ✅ |
| TR-worldeco-008 | 52 的 spawn_anchor 读定义不读状态 | ADR-021 ✅ |
| TR-worldeco-009 | PoiState 枚举具体值 + 状态机合法转移(归 6 的 GDD) | 6 GDD ✅ |
| TR-worldeco-010 | chunk 激活权=6;只读 ActorCellEntered 的格;激活=派生态不进流 | ADR-023 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/world-and-ecozones.md`(AC-6-01…26a)are verified —— A 组格/可走性 · B 组 POI 状态机 · C 组生态区几何 · D 组发现门([L] 与 EXTERNAL 项按各自通道,SIGN-OFF/CI 产物为证)
- All Logic and Integration stories have passing test files
- 6 的白名单消费方({4,25,37},OQ-6-1 已结,27 已移除)联调通过
- 数值轮解冻前机制与合法性断言全部就位;LATTICE_SIZE / K_speed 等值不作为本 epic 阻塞项

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 世界格与可走性基础 —— LATTICE_SIZE / K_TERRAIN_MAX | Logic | **Complete ✅ 2026-10-01**(42 例,二轮评审后) | ADR-015/006/022 |
| 002 | EcozoneOf 整数几何查询 —— 开集/正则化/min(id) 仲裁 | Logic | **Complete ✅ 2026-10-01**(25 例) | ADR-015/014 |
| 003 | POI 定义加载与三态状态机 —— PoiStateChanged 唯一写通道 | Integration | **Complete ✅ 2026-10-01**(**19 例**;含 2026-10-02 补齐的 6 例 host gate 负向夹具;流侧重放半边归 9/45) | ADR-021/009/005 |
| 004 | chunk 激活权与消费边界 —— 发现门、spawn_anchor、白名单 | Integration | **Complete ✅ 2026-10-01**(**10 例**;含 3 例发现门集成,2026-10-03 重写为真驱动链;白名单静态断言**未实现,已撤勾**) | ADR-023/020/021 |
| 005 | 发现体验走查与工具链一致性([L]/EXTERNAL) | Visual-Feel | **Pending — [L]桌面走查未执行([L]/EXTERNAL 真实未跑,非状态漂移)**;draft 见 `production/qa/evidence/world-ecozones/story-005-*.md` | ADR-022/013 |
| 006 | POI 载荷接线 —— `PoiStateChanged` 改走 `IPayloadEncoder`(闭合 B1) | Logic | **Complete ✅ 2026-10-02**(13/13;头体 Status 矛盾已于 2026-10-03 订正) | ADR-029 + ADR-021 |

## Epic Status

**Complete ✅ 2026-10-03**(6/6 story 已实现;story 005 的 [L]/EXTERNAL 走查为**显式未执行**,见下)。

**转 Complete 的两个前置均已满足**:
1. ✅ **评审报告原件已落盘** —— `production/qa/evidence/review-world-ecozones-2026-10-03.md`(一轮)
   与 `review-world-ecozones-round2-2026-10-03.md`(二轮 = 判定基准)。
   ⚠️ 此前 `index.md` 记「评审报告原件缺」,实为**两份早已在库** —— 该残留已于 2026-10-03 订正。
2. ✅ **评审查出的判据缺陷已修并复跑** —— 见下表;复跑证据
   `production/qa/evidence/world-ecozones/editmode-rerun-2026-10-03.md`。

**Test Evidence(2026-10-03 batchmode 实测,逐 fixture)**:`WorldEcozones`
**109 例 = 109 Passed + 0 Failed + 0 Skipped + 0 Inconclusive**(HEAD `a78c27a`):
`ChunkActivationTest` 10 · `EcozoneQueryTest` 25 · `PoiPayloadEncoderTest` 13 · `PoiStateMachineTest` 19 · `WorldLatticeTest` 42。
⚠️ 旧计数(87/87 · 106/106)系**转录摘要且三处互不一致**,**不再作为依据**(承「不得借绿」)。

### 逐条判据缺陷结算(两轮评审 → 当前 HEAD 实测)

| # | 原判定 | 当前 HEAD 实测 | 结算 |
|---|---|---|---|
| **N1** host gate 返回码与 `PoiNotFound` 混同 | 🔴 真缺陷 | `PoiStateMachine.cs:48` 新增 `NotHost`;`:110-111` gate 返 `NotHost`(先于 `:113` 的 id 检查);测试双向钉死 | ✅ **已修(真到位)** |
| **N5** 发现门集成测试缺失/实质弱 | 🔴 取证缺口 → ⚠️ 形式补实质弱 | `chunk_activation_test.cs:152` `drivenByRealCellEntry` 由生产 `CellTransitionDetector` 产**真** `ActorCellEntered`,tick **取自事件**(非测试硬编码)⇒ 断言非恒真 | ✅ **已修(实质闭环)** |
| **N3** 消费白名单 `{4,25,37}` 静态引用断言 | 🔴 AC 可疑 → ✅ 撤勾到位(实现仍缺) | **全仓零断言代码**(`Editor.Tools.Gates/` 仅 2 处注释);27 侧 `Sim/EnemyAI/` **目录不存在** ⇒ 负向夹具无对象;story-004:50 已 `- [ ]` + 写明重开条件 | ⚠️ **判据仍空缺(已诚实撤勾,不阻塞本 epic)** |

**未闭项登记(不阻塞本 epic 转 Complete)**:
- ⚠️ **N3 —— 白名单静态断言**:实缺陷仍开。**重开条件** = 27 的 AI 实现落地时,同批补白名单断言
  + 27 侧负向夹具(story-004 §AC 条下已写死)。**判据当前不可执行**为事实,但**已无虚报**。
- ⏸️ **Story 005 [L] 走查 + 工具 CI(EXTERNAL)**:真实未执行(非漂移),需桌面 Unity 会话;
  该件 C1–C6 全 `Not-RUN`,**不得借 109/109 转绿**。
- ⏸️ **story-004 `World.unity` 构建期扫描**:`World.unity` 文件本身尚不存在(ADR-023 三场景制),
  已诚实撤勾,重开条件 = 该场景创建时同批补。

### 文档卫生订正(2026-10-03,零行为影响)

- `poi_state_machine_test.cs` §已知缺陷 段(原写「未修 `PoiNotFound`」,与同文件已钉死的 `NotHost` **矛盾**)
  → 改为 §历史缺陷(**已闭**)保留闭环记录。
- `story-003`(gate 残留注「未修,归实现轮」)· 走查件 `:48/:55`(误占 story-006 的 `AC-6-27/28`)
  · `story-003:38` / `story-004:49` / `story-006` 五处的 `RebuildFromEvents` 旧名 → `RebuildFromDecoded`。
- `story-006` 头 `Complete ✅` vs 体 `[ ] Pending` 的矛盾 → 体订正为 `[x] Done`(与 player-controller 004/005/006 同型)。

## Next Step

**本 Epic 已 Complete(2026-10-03)** —— 无剩余**本 epic 范围内**的实现工作。
等待事项均属外部主语 / 外部轮次:

- **N3 白名单静态断言** ⇒ 27 的 AI 实现落地时同批补(带 27 侧负向夹具)。
- **Story 005 [L] 走查 + 工具 CI(EXTERNAL)** ⇒ 需桌面 Unity 会话,走查件已备好 checklist。
- **story-004 `World.unity` 构建期扫描** ⇒ ADR-023 三场景制创建 `World.unity` 时同批补。

下一件(Route A / Epic 3):`modular-building`(见 `production/epics/index.md` 与其评审原件)。
