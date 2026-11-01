# S23-1 实施证据 — story-001 PlayerDied 登记与死亡结算入口(2026-11-01)

| 字段 | 值 |
|---|---|
| **对象** | `production/epics/death-respawn/story-001-player-died-settlement.md`(Integration · 5h) |
| **轮次** | sprint-23 · `S23-1`(/dev-story 开工 → 实施 → 证伪 → 回归) |
| **结论** | **生产码零缺口,本卡 = AC 闭夹具**(R-3 复核结论的实测确认)· 9 测全绿 · 证伪 2/2 精确转红 |

---

## 1. 勘察结论:为什么是「零生产缺口」(逐段 file:line 证据)

story-001 的结算管线在 S18-1(谓词/写者,`EntityDeathGates` / `PlayerDiedWriter`)、
story-004(冷却门 `DeathCooldownGate`)、`519700c`(生产构造点)各轮已逐段落地,
开工勘察实测**五段全在且已装配**:

| 段 | 落点 | 状态 |
|---|---|---|
| ① 9 侧候选产出 | `Gameplay.Boot/DiseaseVitalsService.cs:646`(`_deathCandidates.Publish(actorId, tick)`) | ✅ 生产触发点在 |
| ② sink 契约 | `CompositionRootServices.DeathCandidates`(`:121`)+ 单消费者 `Register` | ✅ |
| ③ 消费者(谓词 + 门编排) | `Sim/DeathCandidateConsumer.cs:89-102`(`OnDeathCandidate` → `PlayerDeathWarranted` → 委托写者) | ✅ |
| ④ 写者(最后一道闸 + 唯一 Append) | `Sim/PlayerDiedWriter.cs:99-127`(谓词复核 → 冷却门 → 载荷编码 → 世界流一条) | ✅ |
| ⑤ 生产装配 | `CompositionRoot.cs:340-379`(真冷却门 → 写者 → entry/class/deathCell 查询 → `Register`) | ✅ |

⇒ 实施面 = **测试闭**(story 卡面 Test Evidence 声明的 `player_died_settlement_test.cs`),
无一行生产码改动。这与取件前 §6.3 复核的 R-3 结论(「001 = 补结算链余段」)相比**更进一步**:
余段也已由 S18-1/story-004/`519700c` 补完。

## 2. 落盘件

| 文件 | 内容 |
|---|---|
| `Tests/EditMode/DeathRespawn/player_died_settlement_test.cs`(新,9 测) | AC 闭夹具(见 §3 映射) |
| `production/epics/death-respawn/story-001…md` | Manifest Version 2026-10-02 → **2026-10-15**(dev-story 推荐项 A;死亡层无新增 manifest 规则)· Last Updated 2026-11-01 |
| `production/sprint-status.yaml` | `S23-1` → `in_progress` |

**与既有三件的分工**(文件头有完整映射表,判「无证据」前已扫全树):
`death_candidate_writers_test.cs`(S18-1,桩门下的写者/谓词)· `death_cooldown_test.cs`
(story-004,门边界/登记一致性)· `DiseaseSimulation/death_candidate_sink_test.cs`
(sink 契约 + F4 端到端,含门 A 断言)—— 本件只做它们**没有**的面。

## 3. AC → 测试映射(9 条,全绿 `Logs/s231-drv2.xml` 9/9 @ 2026-11-01)

| AC | 测试 | 增量说明 |
|---|---|---|
| AC-29-01(半边) | `test_candidate_throughRealGate_writesOnePlayerDied` | **真冷却门**(非桩)—— 桩面见 S18-1 件 |
| AC-29-01(幂等) | `test_sameCandidateReinjected_sameTick_idempotent_oneEvent` | 去重 = F-29-2 前缀纯函数承载,零独立计数器(AC-29-15 同纪律) |
| AC-29-03 | 既有 structural(背包无关)+ 本件链路全程无背包依赖 | 不重复,文件头登记 |
| AC-29-13(读法 A) | `test_consumerCtorDependencySet_withinStoryDeclaredShape` | 构造依赖闭包恰 5 形;零 `IEventSink`(单写入口)· 零 `PatientId`(零病人身份谓词) |
| 路由断言 | `test_playerDied_onlyInWorldStream_neverInHistoryOrCase` | TR-death-002 行为面 |
| 主机唯一 Append | `test_noOtherProductionWriter_appendsPlayerDied` | 源码扫描两层判据(见 §5 首轮红) |
| 移动禁用接线 | `test_deathMovementDisable_zeroMotorSuppressedIn29Surface` | 29 结算面五文件零 `MotorSuppressed` + GDD 依赖行存在性(读真 GDD) |
| AC-29-08(004 全链半) | `test_secondCandidateWithinCooldown_zeroEvents` | 负控独立于正例(F4):先断第一条已入流 |
| AC-29-10(004 全链半) | `test_boundedDeaths_overHorizon_playersDividedByCooldown` | 每 tick 一候选 × 2600 tick ⇒ 恰 {0, 2400};⌊H/CD⌋+1 上界 |
| 装配三分(Sim 侧) | 既有 `sink_test.test_simAssembly_referencesZeroEngineAssemblies` | 门 A 断言不重复 |
| death_cell 兜底 | `test_deathCell_fallsBackToSpawnCell_whenNeverCrossed` | 生产 lambda 镜像(`WorldSpawnCell = (0,1,0)`) |

## 4. 证伪轮(S12-4 · 同命令 改→跑→还原 + SHA-256 逐字节)

| 变异 | 靶点 | 实测 |
|---|---|---|
| **M-A** 冷却门短路(消费者 `_deathAllowed(playerId, tick)` → 常量 `true`) | `DeathCandidateConsumer.cs:101` | ✅ **恰红门依赖的 3 条**(幂等 / 冷却内零事件 / 有界性),其余 6 条不受影响;RESTORE-A-OK |
| **M-B** 路由分支改指 History(`EventKind.PlayerDied => StreamId.History`) | `StreamRouting.g.cs:57`(generated,还原纪律同常) | ✅ 恰红路由测 1 条;RESTORE-B-OK |

残留 `grep FALSIFY` 零命中。日志:`unity/Logs/s231-fals{A,B}.{xml,log}`。

## 5. 首轮红(如实登记 —— 扫描判据被真树教育的一次)

`test_noOtherProductionWriter…` 首版断「引用点恰 = 写者 + 路由生成物」,实跑红:
`DeathCooldownGate.cs` 也引用 `EventKind.PlayerDied` —— **合法读侧**(重算 `last_death_tick`
须识别该 Kind;AC-29-15 零计数器的必然推论)。判据改为两层:① 引用点 ⊆ **带理由的允许集**
(新增引用须有意识地登记,不静默扩散);② 允许集内「引用 ∧ `Append(new SimEvent`」共现者
**恰为写者**(读侧长出 Append = 第二写入通道)。改后 9/9。
**这条红是好信号**:扫描判据真的在读真树,不是恒真自述。

## 6. 回归(三元组)

| 轮 | 元素 | 值 |
|---|---|---|
| **截至时刻** | | 2026-11-01 **13:07:10 CST**(EditMode 13:06:29) |
| **批次** | | S23-1(player_died_settlement_test 9 新测;生产码零改动) |
| **commit** | | `1adb1ad8` + 工作树(提交前) |

| 套件 | 结果 | 前次(§9 of S22-9 件) | 等式 |
|---|---|---|---|
| EditMode | `total=6379 passed=6342 failed=0 skipped=37` | `6370 / 6333 / 0 / 37`(11:47:46) | `6379 − 6370 = 9` ✅ |
| PlayMode | `total=273 passed=269 failed=0 skipped=2`(+2 Inconclusive 存量) | `273 / 269 / 0 / 2`(12:14:36) | `±0`(零生产码改动)✅ |

复算命令:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/s231-final-em.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/s231-final-pm.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
```

## 7. 已知边界(不借绿)

- **AC-29-01 仍记「半边」**:本件与既有件的候选注入 = 测试 harness 手动注入;9 侧真件虽已
  在生产(`DiseaseVitalsService:646`),「真病死于真玩家」的全链绿(真实病程 → F4 → 判死 →
  结算)须实机病程跑通 —— **这正是 G-1 门行结案的验收核内容**(归收口轮,不由本卡自裁)。
- `entryOf` 夹具 = 纯数据注册表条目(`LethalEntry()`),非 Fake 被测对象;`actorClassOf` 同理。
- 装配三分的**呈现半**(`IsDowned` 第二 QoS / 呈现订阅)属 story-006,不在本卡面。
