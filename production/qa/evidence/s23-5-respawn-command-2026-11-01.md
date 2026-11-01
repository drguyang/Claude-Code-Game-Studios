# S23-5 实施证据 — story-005 复活落点与传送命令(2026-11-01)

| 字段 | 值 |
|---|---|
| **对象** | `production/epics/death-respawn/story-005-respawn-cell-teleport-command.md`(Integration · 5h) |
| **轮次** | sprint-23 · `S23-5`(实施 → 证伪 → 回归) |
| **结论** | 发令面 + 端口 + 消费者回调位;11 测全绿;证伪 4/4 各自精确转红 |

---

## 1. 勘察判定与接线边界(如实登记)

- `ITeleportCommandSink`(ADR-025 QQ-01 ①′)在盘上为**有契约、零生产实装、零生产调用**
  —— 1 侧(玩家控制器/表现)未建。按卡面 guardrail「接口先行 + 桩面明确标注」:
  **本卡交付 = 29 侧发令面 + `IClinicReviveCell` 端口 + 消费者第四回调位**,
  **生产装配不接线**(不伪造 1 侧 sink —— 那是 F2 族假接线);生产接线义务 = 1 侧
  传送实装轮的装配段(届时 `revive:` 一参即可)。
- `IClinicReviveCell` 住 `Sim`(消费方与提供方同程序集,与 `IDeathCandidateSource` 同格);
  **实现归 24 运行期查询面**(BLOCKED-BY-24 = 实现半;选定规则本体已落 24 GDD 规则六,
  设计半已清 —— 卡面 2026-11-01 R-2 注)。
- 床格端口生产传 `null` 与否:生产装配**未构造**发令面(见上),装配轮接线时按
  24 实装与否决定 `clinicCellOf` 实参;兜底出生点 = `CompositionRoot.WorldSpawnCell`
  (ADR-015 固定格,同 deathCellOf 兜底源)。

## 2. 落盘件

| 文件 | 内容 |
|---|---|
| `Sim/RespawnCommandIssuer.cs`(新) | 发令面:落点两分支(床格端口 → 兜底出生点)→ `ITeleportCommandSink.RequestTeleport(int, WorldPos)`;**零 IEventSink 触点、零流事件**(AC-29-09;复活不重置疾病/世界 —— 副作用负面清单的结构性面);`IClinicReviveCell` 端口同文件 |
| `Sim/DeathCandidateConsumer.cs` | 结算段末步 `revive` 可选回调(`PlayerDied → 掉落 → 掉级 → 复活`;仅 `Written` 后) |
| `Tests/EditMode/DeathRespawn/death_respawn_command_test.cs`(新,11 测) | 见 §3;sink = 显式命名测试桩(1 侧未建,桩面标注) |
| story-005 卡 + sprint-status.yaml | 开工登记 |

## 3. AC → 测试映射(11 条,全绿 `Logs/s235-t1.xml` @ 2026-11-01 15:01)

| AC | 测试 |
|---|---|
| 落点两分支 | `test_clinicBedAvailable_commandTargetsBedCell` + `test_noClinicBothForms_commandTargetsSpawnCell`(端口未接线 null / 端口答无 两形态) |
| 联机共床 | `test_twoPlayersSameBed_sameCellTwoCommands`(两玩家同床格 ⇒ 同格两命令,逐 actor) |
| AC-29-09(检索半) | `test_ac2909_zeroAppendPoint_in29RespawnPath`(发令面 + 消费者剥注释:零 `.Append(` / 零 `IEventSink` / 零 `ActorCellEntered`) |
| AC-29-09(行为半)+ 副作用负面清单 | `test_issue_zeroStreamEvents_sideEffectNegativeList`(发令零流事件;疾病/世界/掉落全不残不越) |
| AC-29-10(半边) | `test_boundedRevives_throughRealChain_commandsTrackDeaths`(真冷却门链:候选 0..2600 ⇒ 死亡 {0,2400} ⇒ 复活命令恰 2;**ActorCellEntered 实发数归 1 侧联调,收口核**) |
| 门控 | `test_rejectOrCooldown_zeroCommands`(同 candidate 幂等 ⇒ 恰一命令) |
| 结算段时序 | `test_settlementSegment_reviveAfterSkillDown`(掉落 → 掉级 → 复活 spy 全序) |
| 未接线 | `test_consumerUnwired_revive_003BehaviorPreserved` |
| 命令形状(ADR-025 引用面) | `test_commandShape_allInteger_zeroVector3`(契约 + 发令面零 `Vector3`/`float`/浮点字面量;正判 `RequestTeleport(int actorId, WorldPos cell)`) |
| 参数守卫 | `test_issue_invalidActor_throws` |

## 4. 证伪轮(S12-4 · 同命令 改→跑→还原 + SHA-256 逐字节)

| 变异 | 靶点 | 实测 |
|---|---|---|
| **M-A** 落点分支失效(恒兜底出生点) | `RespawnCommandIssuer.cs` 落点选择式 | ✅ 恰红 **2** —— `clinicBedAvailable` + `twoPlayersSameBed`(两分支判别:正例红、兜底负例仍绿);RESTORE-OK `58f3c36d9715` |
| **M-B** 段序颠倒(复活先于掉级) | `DeathCandidateConsumer.cs` 结算段 | ✅ 恰红 `reviveAfterSkillDown`(定序类);RESTORE-OK `bcf65b6ae119` |
| **M-C** `Written` 门外泄(谓词拒/冷却拒也发复活) | `DeathCandidateConsumer.cs`(invocation 移到守卫外) | ✅ 红 3 —— `boundedRevives`(命令 2601 ≠ 2)+ `rejectOrCooldown`(2 ≠ 1)+ `reviveAfter`(spy 序多一条);红集合 = 门控语义的依赖面;RESTORE-OK `bcf65b6ae119` |
| **M-D** 命令不发(no-op) | `RespawnCommandIssuer.cs`(RequestTeleport 剥除) | ✅ 红 6 —— 全部命令面夹具;RESTORE-OK `58f3c36d9715` |

四个红集合各自覆盖其守卫(M-C/M-D 共享面为门控×发射的正交耦合,方向一致)。
残留 `grep FALSIFY` 零命中。日志:`unity/Logs/s235-fals{A,B,C,D}.{xml,log}`。

## 5. 回归(三元组 · 终值)

| 轮 | 元素 | 值 |
|---|---|---|
| **截至时刻** | | 2026-11-01 **15:07:31 CST**(EditMode)/ 15:08:37 CST(PlayMode) |
| **批次** | | S23-5(发令面 + 端口 + 消费者第四回调位 + 11 新测) |
| **commit** | | 本卡提交(见 git log S23-5) |

| 套件 | 结果 | 前次(S23-3 终值) | 等式 |
|---|---|---|---|
| EditMode | `total=6415 passed=6377 failed=1 skipped=37` | `6404 / 6367 / 0 / 37`(14:53:18) | `6415 − 6404 = 11` ✅(11 新测;红 1 见下) |
| PlayMode | `total=273 passed=269 failed=0 skipped=2` | `273 / 269 / 0 / 2` | `±0` ✅ |

> **中间态如实登记**:该轮 `failed=1` = S23-1 的 `test_consumerCtorDependencySet_withinStoryDeclaredShape`
> 拦下本卡对构造依赖闭包的第二批扩位(`revive` 回调位)。**门是对的**;修法 = 入集
> 「+S23-5 复活回调位」。修复后定向 `PlayerDiedSettlementTest` **9/9 绿**(`Logs/s236-t2.xml`
> @ 15:15:20);**干净全量确认随 S23-6 的终值回归**(其 EditMode 含本修复,`failed=0`)。
> 复算命令:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/s235-em.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/s235-pm.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
```

## 6. 已知边界(不借绿)

- **AC-29-10 记「半边」**:复活命令数 == PlayerDied 数已证;`ActorCellEntered` 实发
  (1 侧消费)联调维持 BLOCKED-BY-9/24,G-1 门行收口核一并。
- **生产接线未落**:发令面零生产构造点(1 侧传送实装轮的装配义务;届时
  `revive: (id, tick) => respawnIssuer.Issue(id)` 一参接上)。本卡不伪造接线。
- **床格选定规则本体** = 24 GDD 规则六(`(y,x,z)` min);24 运行期查询面实现 + 装配
  归 24 轮 —— 本卡的 `IClinicReviveCell` 端口对实现零假设(只约「有/无」与「格」)。
- **复活时点语义**:本卡按卡面「死亡结算段末步」即时发令(GDD 规则五);
  「玩家选择延迟复活」类设计未出现于 GDD,若引入 = 另裁。
